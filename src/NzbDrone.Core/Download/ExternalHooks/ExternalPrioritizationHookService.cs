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
    public class ExternalPrioritizationHookService : IExternalPrioritizationHookService
    {
        private readonly IConfigService _configService;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public ExternalPrioritizationHookService(
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

        public async Task<ExternalPrioritizationResult> EvaluateAsync(List<DownloadDecision> decisions)
        {
            if (!_configService.ExternalPrioritizationHookEnabled)
            {
                return ExternalPrioritizationResult.Proceed(decisions);
            }

            var url = _configService.ExternalPrioritizationHookUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.Debug("External prioritization hook enabled but URL is empty, skipping");
                return ExternalPrioritizationResult.Proceed(decisions);
            }

            // Check skip tag
            var skipTag = _configService.ExternalHooksSkipTag;
            if (!string.IsNullOrWhiteSpace(skipTag) && decisions.Count > 0)
            {
                var movie = decisions[0].RemoteMovie.Movie;
                if (movie.Tags != null && movie.Tags.Any(t => t.ToString() == skipTag))
                {
                    _logger.Debug("Movie '{0}' has skip tag '{1}', bypassing external prioritization hook", movie.MovieMetadata.Value.Title, skipTag);
                    return ExternalPrioritizationResult.Proceed(decisions);
                }
            }

            try
            {
                var radarrTopPickGuid = decisions.Count > 0 ? decisions[0].RemoteMovie.Release.Guid : null;

                var payload = ExternalHookPayloadBuilder.Build(
                    "prioritization",
                    _configFileProvider.InstanceName,
                    _configService.ApplicationUrl,
                    decisions,
                    radarrTopPickGuid);

                if (payload == null)
                {
                    return ExternalPrioritizationResult.Proceed(decisions);
                }

                var response = await CallWebhookAsync(url, payload);
                return ProcessResponse(decisions, response);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "External prioritization hook failed, proceeding with Radarr's default order (fail-open)");
                return ExternalPrioritizationResult.Proceed(decisions);
            }
        }

        private async Task<ExternalPrioritizationHookResponse> CallWebhookAsync(string url, ExternalHookPayload payload)
        {
            var timeout = _configService.ExternalPrioritizationHookTimeout;
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

            _logger.Debug("Calling external prioritization hook at {0} with {1} releases (top pick: {2})",
                url, payload.Releases.Count, payload.RadarrTopPickGuid);

            var httpResponse = await _httpClient.ExecuteAsync(request);

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new ExternalHookException($"External prioritization hook returned HTTP {(int)httpResponse.StatusCode}");
            }

            var responseBody = httpResponse.Content;
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                _logger.Debug("External prioritization hook returned empty response, using Radarr's default order");
                return new ExternalPrioritizationHookResponse();
            }

            return Json.Deserialize<ExternalPrioritizationHookResponse>(responseBody);
        }

        private ExternalPrioritizationResult ProcessResponse(List<DownloadDecision> decisions, ExternalPrioritizationHookResponse response)
        {
            if (response == null)
            {
                return ExternalPrioritizationResult.Proceed(decisions);
            }

            // Handle defer
            if (response.Defer == true && response.DeferMinutes.HasValue && response.DeferMinutes.Value > 0)
            {
                _logger.Info("External prioritization hook requested defer for {0} minutes: {1}",
                    response.DeferMinutes.Value, response.Reason ?? "no reason given");
                return ExternalPrioritizationResult.Defer(response.DeferMinutes.Value, response.Reason ?? "Deferred by external hook");
            }

            // Handle selectedGuid
            if (!string.IsNullOrWhiteSpace(response.SelectedGuid))
            {
                var selectedDecision = decisions.FirstOrDefault(d =>
                    string.Equals(d.RemoteMovie.Release.Guid, response.SelectedGuid, StringComparison.OrdinalIgnoreCase));

                if (selectedDecision == null)
                {
                    _logger.Warn("External prioritization hook selected GUID '{0}' not found among {1} releases, using Radarr's default order",
                        response.SelectedGuid, decisions.Count);
                    return ExternalPrioritizationResult.Proceed(decisions);
                }

                _logger.Info("External prioritization hook selected release: '{0}' (was #{1} in Radarr's order)",
                    selectedDecision.RemoteMovie.Release.Title,
                    decisions.IndexOf(selectedDecision) + 1);

                var reordered = new List<DownloadDecision> { selectedDecision };
                reordered.AddRange(decisions.Where(d => d != selectedDecision));
                return ExternalPrioritizationResult.Proceed(reordered);
            }

            // Handle orderedGuids
            if (response.OrderedGuids != null && response.OrderedGuids.Count > 0)
            {
                var guidToDecision = decisions.ToDictionary(d => d.RemoteMovie.Release.Guid, StringComparer.OrdinalIgnoreCase);
                var reordered = new List<DownloadDecision>();

                foreach (var guid in response.OrderedGuids)
                {
                    if (guidToDecision.TryGetValue(guid, out var decision))
                    {
                        reordered.Add(decision);
                        guidToDecision.Remove(guid);
                    }
                    else
                    {
                        _logger.Warn("External prioritization hook referenced unknown GUID '{0}', skipping", guid);
                    }
                }

                // Append any decisions not mentioned in the response (preserve order for safety)
                reordered.AddRange(guidToDecision.Values);

                _logger.Info("External prioritization hook reordered {0} releases", reordered.Count);
                return ExternalPrioritizationResult.Proceed(reordered);
            }

            // Empty response — keep Radarr's default
            _logger.Debug("External prioritization hook returned no selection, using Radarr's default order");
            return ExternalPrioritizationResult.Proceed(decisions);
        }
    }
}
