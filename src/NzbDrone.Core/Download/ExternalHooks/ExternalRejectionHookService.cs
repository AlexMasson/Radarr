using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;

namespace NzbDrone.Core.Download.ExternalHooks
{
    public class ExternalRejectionHookService : IExternalRejectionHookService
    {
        private readonly IConfigService _configService;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public ExternalRejectionHookService(
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

        public async Task<ExternalRejectionResult> EvaluateAsync(List<DownloadDecision> decisions)
        {
            if (!_configService.ExternalRejectionHookEnabled)
            {
                return new ExternalRejectionResult(decisions, new List<DownloadDecision>());
            }

            var url = _configService.ExternalRejectionHookUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.Debug("External rejection hook enabled but URL is empty, skipping");
                return new ExternalRejectionResult(decisions, new List<DownloadDecision>());
            }

            // Check skip tag
            var skipTag = _configService.ExternalHooksSkipTag;
            if (!string.IsNullOrWhiteSpace(skipTag) && decisions.Count > 0)
            {
                var movie = decisions[0].RemoteMovie.Movie;
                if (movie.Tags != null && movie.Tags.Any(t => t.ToString() == skipTag))
                {
                    _logger.Debug("Movie '{0}' has skip tag '{1}', bypassing external rejection hook", movie.MovieMetadata.Value.Title, skipTag);
                    return new ExternalRejectionResult(decisions, new List<DownloadDecision>());
                }
            }

            try
            {
                var payload = ExternalHookPayloadBuilder.Build(
                    "rejection",
                    _configFileProvider.InstanceName,
                    _configService.ApplicationUrl,
                    decisions);

                if (payload == null)
                {
                    return new ExternalRejectionResult(decisions, new List<DownloadDecision>());
                }

                var response = await CallWebhookAsync(url, payload);
                return ProcessResponse(decisions, response);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "External rejection hook failed, proceeding with all releases (fail-open)");
                return new ExternalRejectionResult(decisions, new List<DownloadDecision>());
            }
        }

        private async Task<ExternalRejectionHookResponse> CallWebhookAsync(string url, ExternalHookPayload payload)
        {
            var timeout = _configService.ExternalRejectionHookTimeout;
            var username = _configService.ExternalHooksUsername;
            var password = _configService.ExternalHooksPassword;

            var request = new HttpRequest(url)
            {
                Method = HttpMethod.Post,
                Headers = { ContentType = "application/json", Accept = "application/json" }
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                request.Credentials = new BasicNetworkCredential(username, password);
            }

            request.SetContent(payload.ToJson());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            _logger.Debug("Calling external rejection hook at {0} with {1} releases", url, payload.Releases.Count);

            var httpResponse = await _httpClient.ExecuteAsync(request);

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new ExternalHookException($"External rejection hook returned HTTP {(int)httpResponse.StatusCode}");
            }

            var responseBody = httpResponse.Content;
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                _logger.Debug("External rejection hook returned empty response, keeping all releases");
                return new ExternalRejectionHookResponse { Decisions = new List<ExternalRejectionDecision>() };
            }

            return Json.Deserialize<ExternalRejectionHookResponse>(responseBody);
        }

        private ExternalRejectionResult ProcessResponse(List<DownloadDecision> decisions, ExternalRejectionHookResponse response)
        {
            if (response?.Decisions == null || response.Decisions.Count == 0)
            {
                _logger.Debug("External rejection hook returned no decisions, keeping all releases");
                return new ExternalRejectionResult(decisions, new List<DownloadDecision>());
            }

            var rejectionMap = response.Decisions
                .Where(d => d.Rejected)
                .ToDictionary(d => d.Guid, d => d.Reason ?? "Rejected by external hook", StringComparer.OrdinalIgnoreCase);

            var accepted = new List<DownloadDecision>();
            var rejected = new List<DownloadDecision>();

            foreach (var decision in decisions)
            {
                var guid = decision.RemoteMovie.Release.Guid;

                if (rejectionMap.TryGetValue(guid, out var reason))
                {
                    _logger.Info("Release '{0}' rejected by external hook: {1}", decision.RemoteMovie.Release.Title, reason);

                    var rejection = new DownloadRejection(
                        DownloadRejectionReason.ExternalHookRejection,
                        $"[External] {reason}",
                        RejectionType.Permanent);

                    var rejectedDecision = new DownloadDecision(decision.RemoteMovie, rejection);
                    rejected.Add(rejectedDecision);
                }
                else
                {
                    accepted.Add(decision);
                }
            }

            _logger.Debug("External rejection hook: {0} accepted, {1} rejected out of {2} releases",
                accepted.Count, rejected.Count, decisions.Count);

            return new ExternalRejectionResult(accepted, rejected);
        }
    }
}
