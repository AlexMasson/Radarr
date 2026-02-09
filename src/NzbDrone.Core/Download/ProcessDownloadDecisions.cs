using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.ExternalHooks;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Download
{
    public interface IProcessDownloadDecisions
    {
        Task<ProcessedDecisions> ProcessDecisions(List<DownloadDecision> decisions);
        Task<ProcessedDecisionResult> ProcessDecision(DownloadDecision decision, int? downloadClientId);
    }

    public class ProcessDownloadDecisions : IProcessDownloadDecisions
    {
        private readonly IDownloadService _downloadService;
        private readonly IPrioritizeDownloadDecision _prioritizeDownloadDecision;
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IExternalRejectionHookService _externalRejectionHookService;
        private readonly IExternalPrioritizationHookService _externalPrioritizationHookService;
        private readonly Logger _logger;

        public ProcessDownloadDecisions(IDownloadService downloadService,
                                        IPrioritizeDownloadDecision prioritizeDownloadDecision,
                                        IPendingReleaseService pendingReleaseService,
                                        IExternalRejectionHookService externalRejectionHookService,
                                        IExternalPrioritizationHookService externalPrioritizationHookService,
                                        Logger logger)
        {
            _downloadService = downloadService;
            _prioritizeDownloadDecision = prioritizeDownloadDecision;
            _pendingReleaseService = pendingReleaseService;
            _externalRejectionHookService = externalRejectionHookService;
            _externalPrioritizationHookService = externalPrioritizationHookService;
            _logger = logger;
        }

        public async Task<ProcessedDecisions> ProcessDecisions(List<DownloadDecision> decisions)
        {
            var qualifiedReports = GetQualifiedReports(decisions);
            var rejected = decisions.Where(d => d.Rejected).ToList();

            // External Rejection Hook: filter releases before prioritization
            qualifiedReports = await ApplyExternalRejectionHookAsync(qualifiedReports, rejected);

            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisionsForMovies(qualifiedReports);

            // External Prioritization Hook: reorder releases after Radarr's sorting
            prioritizedDecisions = await ApplyExternalPrioritizationHookAsync(prioritizedDecisions);

            var grabbed = new List<DownloadDecision>();
            var pending = new List<DownloadDecision>();

            var pendingAddQueue = new List<Tuple<DownloadDecision, PendingReleaseReason>>();

            var usenetFailed = false;
            var torrentFailed = false;

            foreach (var report in prioritizedDecisions)
            {
                var downloadProtocol = report.RemoteMovie.Release.DownloadProtocol;

                // Skip if already grabbed
                if (IsMovieProcessed(grabbed, report))
                {
                    continue;
                }

                if (report.TemporarilyRejected)
                {
                    PreparePending(pendingAddQueue, grabbed, pending, report, PendingReleaseReason.Delay);
                    continue;
                }

                if ((downloadProtocol == DownloadProtocol.Usenet && usenetFailed) ||
                    (downloadProtocol == DownloadProtocol.Torrent && torrentFailed))
                {
                    PreparePending(pendingAddQueue, grabbed, pending, report, PendingReleaseReason.DownloadClientUnavailable);
                    continue;
                }

                var result = await ProcessDecisionInternal(report);

                switch (result)
                {
                    case ProcessedDecisionResult.Grabbed:
                        {
                            grabbed.Add(report);
                            break;
                        }

                    case ProcessedDecisionResult.Pending:
                        {
                            PreparePending(pendingAddQueue, grabbed, pending, report, PendingReleaseReason.Delay);
                            break;
                        }

                    case ProcessedDecisionResult.Rejected:
                        {
                            rejected.Add(report);
                            break;
                        }

                    case ProcessedDecisionResult.Failed:
                        {
                            PreparePending(pendingAddQueue, grabbed, pending, report, PendingReleaseReason.DownloadClientUnavailable);

                            if (downloadProtocol == DownloadProtocol.Usenet)
                            {
                                usenetFailed = true;
                            }
                            else if (downloadProtocol == DownloadProtocol.Torrent)
                            {
                                torrentFailed = true;
                            }

                            break;
                        }

                    case ProcessedDecisionResult.Skipped:
                        {
                            break;
                        }
                }
            }

            if (pendingAddQueue.Any())
            {
                _pendingReleaseService.AddMany(pendingAddQueue);
            }

            return new ProcessedDecisions(grabbed, pending, rejected);
        }

        public async Task<ProcessedDecisionResult> ProcessDecision(DownloadDecision decision, int? downloadClientId)
        {
            if (decision == null)
            {
                return ProcessedDecisionResult.Skipped;
            }

            if (!IsQualifiedReport(decision))
            {
                return ProcessedDecisionResult.Rejected;
            }

            if (decision.TemporarilyRejected)
            {
                _pendingReleaseService.Add(decision, PendingReleaseReason.Delay);

                return ProcessedDecisionResult.Pending;
            }

            var result = await ProcessDecisionInternal(decision, downloadClientId);

            if (result == ProcessedDecisionResult.Failed)
            {
                _pendingReleaseService.Add(decision, PendingReleaseReason.DownloadClientUnavailable);
            }

            return result;
        }

        internal List<DownloadDecision> GetQualifiedReports(IEnumerable<DownloadDecision> decisions)
        {
            return decisions.Where(IsQualifiedReport).ToList();
        }

        internal bool IsQualifiedReport(DownloadDecision decision)
        {
            // Process both approved and temporarily rejected
            return (decision.Approved || decision.TemporarilyRejected) && decision.RemoteMovie.Movie != null;
        }

        private bool IsMovieProcessed(List<DownloadDecision> decisions, DownloadDecision report)
        {
            var movieId = report.RemoteMovie.Movie.Id;

            return decisions.Select(r => r.RemoteMovie.Movie)
                            .Select(e => e.Id)
                            .ToList()
                            .Contains(movieId);
        }

        private void PreparePending(List<Tuple<DownloadDecision, PendingReleaseReason>> queue, List<DownloadDecision> grabbed, List<DownloadDecision> pending, DownloadDecision report, PendingReleaseReason reason)
        {
            // If a release was already grabbed with a matching movie we should store it as a fallback
            // and filter it out the next time it is processed.
            // If a higher quality release failed to add to the download client, but a lower quality release
            // was sent to another client we still list it normally so it apparent that it'll grab next time.
            // Delayed is treated the same, but only the first is listed the subsequent items as stored as Fallback.
            if (IsMovieProcessed(grabbed, report) ||
                IsMovieProcessed(pending, report))
            {
                reason = PendingReleaseReason.Fallback;
            }

            queue.Add(Tuple.Create(report, reason));
            pending.Add(report);
        }

        private async Task<ProcessedDecisionResult> ProcessDecisionInternal(DownloadDecision decision, int? downloadClientId = null)
        {
            var remoteMovie = decision.RemoteMovie;
            var remoteIndexer = remoteMovie.Release.Indexer;

            try
            {
                _logger.Trace("Grabbing release '{0}' from Indexer {1} at priority {2}.", remoteMovie, remoteIndexer, remoteMovie.Release.IndexerPriority);
                await _downloadService.DownloadReport(remoteMovie, downloadClientId);

                return ProcessedDecisionResult.Grabbed;
            }
            catch (ReleaseUnavailableException)
            {
                _logger.Warn("Failed to download release '{0}' from Indexer {1}. Release not available", remoteMovie, remoteIndexer);
                return ProcessedDecisionResult.Rejected;
            }
            catch (Exception ex)
            {
                if (ex is DownloadClientUnavailableException || ex is DownloadClientAuthenticationException)
                {
                    _logger.Debug(ex, "Failed to send release '{0}' from Indexer {1} to download client, storing until later.", remoteMovie, remoteIndexer);

                    return ProcessedDecisionResult.Failed;
                }
                else
                {
                    _logger.Warn(ex, "Couldn't add release '{0}' from Indexer {1} to download queue.", remoteMovie, remoteIndexer);
                    return ProcessedDecisionResult.Skipped;
                }
            }
        }

        private async Task<List<DownloadDecision>> ApplyExternalRejectionHookAsync(
            List<DownloadDecision> qualifiedReports,
            List<DownloadDecision> rejected)
        {
            var approvedOnly = qualifiedReports.Where(d => d.Approved).ToList();

            if (approvedOnly.Count == 0)
            {
                return qualifiedReports;
            }

            // Group by movie and evaluate each group
            var movieGroups = approvedOnly.GroupBy(d => d.RemoteMovie.Movie.Id).ToList();
            var allAccepted = new List<DownloadDecision>();

            foreach (var group in movieGroups)
            {
                var result = await _externalRejectionHookService.EvaluateAsync(group.ToList());
                allAccepted.AddRange(result.Accepted);
                rejected.AddRange(result.Rejected);
            }

            // Combine accepted with any temporarily rejected reports (they bypass the hook)
            var temporarilyRejected = qualifiedReports.Where(d => d.TemporarilyRejected).ToList();
            allAccepted.AddRange(temporarilyRejected);

            return allAccepted;
        }

        private async Task<List<DownloadDecision>> ApplyExternalPrioritizationHookAsync(
            List<DownloadDecision> prioritizedDecisions)
        {
            if (prioritizedDecisions.Count == 0)
            {
                return prioritizedDecisions;
            }

            // Group by movie and evaluate each group
            var movieGroups = prioritizedDecisions
                .Where(d => d.RemoteMovie.Movie != null)
                .GroupBy(d => d.RemoteMovie.Movie.Id)
                .ToList();

            var result = new List<DownloadDecision>();

            foreach (var group in movieGroups)
            {
                var groupList = group.ToList();
                var prioritizationResult = await _externalPrioritizationHookService.EvaluateAsync(groupList);

                if (prioritizationResult.ShouldProceed)
                {
                    result.AddRange(prioritizationResult.ModifiedDecisions);
                }
                else if (prioritizationResult.DeferMinutes.HasValue)
                {
                    // Mark all as temporarily rejected for delay
                    foreach (var decision in groupList)
                    {
                        var rejection = new DownloadRejection(
                            DownloadRejectionReason.ExternalHookRejection,
                            $"[External] Deferred: {prioritizationResult.Reason}",
                            RejectionType.Temporary);

                        result.Add(new DownloadDecision(decision.RemoteMovie, rejection));
                    }
                }
            }

            // Add any decisions without a movie (shouldn't happen but safety)
            result.AddRange(prioritizedDecisions.Where(d => d.RemoteMovie.Movie == null));

            return result;
        }
    }
}
