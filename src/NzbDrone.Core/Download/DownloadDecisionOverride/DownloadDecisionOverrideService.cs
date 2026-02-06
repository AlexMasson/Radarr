using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.Download.DownloadDecisionOverride
{
    public class DownloadDecisionOverrideService : IDownloadDecisionOverrideService
    {
        private readonly IConfigService _configService;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public DownloadDecisionOverrideService(
            IConfigService configService,
            IConfigFileProvider configFileProvider,
            IHttpClient httpClient,
            Logger logger)
        {
            _configService = configService;
            _configFileProvider = configFileProvider;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<DownloadDecisionOverrideResult> EvaluateAsync(
            Movie movie,
            List<DownloadDecision> prioritizedDecisions,
            CancellationToken cancellationToken = default)
        {
            // If download decision override is not enabled, proceed normally
            if (!_configService.DownloadDecisionOverrideEnabled)
            {
                return DownloadDecisionOverrideResult.Proceed(prioritizedDecisions);
            }

            var webhookUrl = _configService.DownloadDecisionOverrideUrl;

            if (webhookUrl.IsNullOrWhiteSpace())
            {
                _logger.Debug("Download decision override enabled but URL not configured, proceeding with default selection");
                return DownloadDecisionOverrideResult.Proceed(prioritizedDecisions);
            }

            var timeout = TimeSpan.FromSeconds(_configService.DownloadDecisionOverrideTimeout);

            try
            {
                var payload = BuildPayload(movie, prioritizedDecisions);
                var response = await CallWebhookAsync(webhookUrl, payload, timeout, cancellationToken);
                return ProcessResponse(response, prioritizedDecisions, movie);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.Warn("Download decision override cancelled, proceeding with default selection for '{0}'", movie.Title);
                return DownloadDecisionOverrideResult.Proceed(prioritizedDecisions);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Download decision override failed for '{0}', proceeding with default selection", movie.Title);
                return DownloadDecisionOverrideResult.Proceed(prioritizedDecisions);
            }
        }

        private DownloadDecisionOverridePayload BuildPayload(Movie movie, List<DownloadDecision> decisions)
        {
            var isFirst = true;
            var releases = new List<DownloadDecisionOverrideReleaseResource>();

            foreach (var decision in decisions)
            {
                releases.Add(new DownloadDecisionOverrideReleaseResource(decision.RemoteMovie, isFirst));
                isFirst = false;
            }

            return new DownloadDecisionOverridePayload
            {
                InstanceName = _configFileProvider.InstanceName,
                ApplicationUrl = _configService.ApplicationUrl,
                Movie = new DownloadDecisionOverrideMovieResource
                {
                    Id = movie.Id,
                    Title = movie.Title,
                    Year = movie.Year,
                    FolderPath = movie.Path,
                    TmdbId = movie.TmdbId,
                    ImdbId = movie.ImdbId
                },
                Releases = releases
            };
        }

        private async Task<DownloadDecisionOverrideResponse> CallWebhookAsync(
            string url,
            DownloadDecisionOverridePayload payload,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _logger.Debug("Calling download decision override webhook at '{0}' for movie '{1}' with {2} releases",
                          url,
                          payload.Movie.Title,
                          payload.Releases.Count);

            var request = new HttpRequestBuilder(url)
                .Accept(HttpAccept.Json)
                .Build();

            request.Method = HttpMethod.Post;
            request.Headers.ContentType = "application/json";
            request.SetContent(payload.ToJson());
            request.RequestTimeout = timeout;

            // Add basic auth if configured
            var username = _configService.DownloadDecisionOverrideUsername;
            var password = _configService.DownloadDecisionOverridePassword;

            if (username.IsNotNullOrWhiteSpace() || password.IsNotNullOrWhiteSpace())
            {
                request.Credentials = new BasicNetworkCredential(username, password);
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(timeout);

            var response = await _httpClient.ExecuteAsync(request);

            if (!response.HasHttpError)
            {
                return Json.Deserialize<DownloadDecisionOverrideResponse>(response.Content);
            }

            throw new DownloadDecisionOverrideException(
                "Download decision override webhook returned HTTP {0}: {1}",
                response.StatusCode,
                response.Content);
        }

        private DownloadDecisionOverrideResult ProcessResponse(
            DownloadDecisionOverrideResponse response,
            List<DownloadDecision> originalDecisions,
            Movie movie)
        {
            if (response == null)
            {
                _logger.Warn("Download decision override returned null response for '{0}', proceeding with default selection", movie.Title);
                return DownloadDecisionOverrideResult.Proceed(originalDecisions);
            }

            if (!response.Approved)
            {
                var reason = response.Reason.IsNotNullOrWhiteSpace()
                    ? response.Reason
                    : "Rejected by download decision override";

                if (response.DeferMinutes.HasValue && response.DeferMinutes.Value > 0)
                {
                    _logger.Info(
                        "Download decision override deferred grab for '{0}' by {1} minutes: {2}",
                        movie.Title,
                        response.DeferMinutes.Value,
                        reason);
                    return DownloadDecisionOverrideResult.Defer(response.DeferMinutes.Value, reason);
                }

                _logger.Info("Download decision override rejected all releases for '{0}': {1}", movie.Title, reason);
                return DownloadDecisionOverrideResult.Reject(reason);
            }

            // Check if a specific release was selected
            if (response.SelectedReleaseGuid.IsNotNullOrWhiteSpace())
            {
                var selectedDecision = originalDecisions
                    .FirstOrDefault(d => d.RemoteMovie.Release.Guid == response.SelectedReleaseGuid);

                if (selectedDecision != null)
                {
                    _logger.Info(
                        "Download decision override selected release '{0}' for '{1}'",
                        selectedDecision.RemoteMovie.Release.Title,
                        movie.Title);

                    // Move selected release to front of list
                    var reorderedList = new List<DownloadDecision> { selectedDecision };
                    reorderedList.AddRange(originalDecisions.Where(d => d != selectedDecision));
                    return DownloadDecisionOverrideResult.Proceed(reorderedList);
                }

                _logger.Warn(
                    "Download decision override selected GUID '{0}' not found in releases for '{1}', using default",
                    response.SelectedReleaseGuid,
                    movie.Title);
            }

            // Check if a custom order was provided
            if (response.ReleaseOrder != null && response.ReleaseOrder.Any())
            {
                var reorderedList = new List<DownloadDecision>();
                var remaining = new List<DownloadDecision>(originalDecisions);

                foreach (var guid in response.ReleaseOrder)
                {
                    var decision = remaining.FirstOrDefault(d => d.RemoteMovie.Release.Guid == guid);
                    if (decision != null)
                    {
                        reorderedList.Add(decision);
                        remaining.Remove(decision);
                    }
                }

                // Add any decisions not in the order list at the end
                reorderedList.AddRange(remaining);

                _logger.Debug(
                    "Download decision override reordered {0} releases for '{1}'",
                    response.ReleaseOrder.Count,
                    movie.Title);
                return DownloadDecisionOverrideResult.Proceed(reorderedList);
            }

            _logger.Debug("Download decision override approved default selection for '{0}'", movie.Title);
            return DownloadDecisionOverrideResult.Proceed(originalDecisions);
        }
    }
}
