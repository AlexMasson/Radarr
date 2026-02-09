using System;
using System.Collections.Generic;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download.ExternalHooks
{
    public class ExternalHookPayload
    {
        public string HookType { get; set; }
        public string InstanceName { get; set; }
        public string ApplicationUrl { get; set; }
        public ExternalHookMovieResource Movie { get; set; }
        public List<ExternalHookReleaseResource> Releases { get; set; }
        public string RadarrTopPickGuid { get; set; }
    }

    public class ExternalHookMovieResource
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string FolderPath { get; set; }
        public int TmdbId { get; set; }
        public string ImdbId { get; set; }
        public string QualityProfileName { get; set; }
        public int QualityProfileId { get; set; }
        public List<string> Tags { get; set; }
    }

    public class ExternalHookReleaseResource
    {
        public string Guid { get; set; }
        public string Title { get; set; }
        public string Indexer { get; set; }
        public int IndexerId { get; set; }
        public int IndexerPriority { get; set; }
        public string Quality { get; set; }
        public int QualityVersion { get; set; }
        public string ReleaseGroup { get; set; }
        public List<string> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public long Size { get; set; }
        public string Protocol { get; set; }
        public int? Seeders { get; set; }
        public int? Leechers { get; set; }
        public double AgeMinutes { get; set; }
        public DateTime? PublishDate { get; set; }
        public List<string> Languages { get; set; }
        public List<string> IndexerFlags { get; set; }
        public bool IsRadarrTopPick { get; set; }
    }

    public static class ExternalHookPayloadBuilder
    {
        public static ExternalHookPayload Build(
            string hookType,
            string instanceName,
            string applicationUrl,
            List<DownloadDecision> decisions,
            string radarrTopPickGuid = null)
        {
            if (decisions == null || decisions.Count == 0)
            {
                return null;
            }

            var movie = decisions[0].RemoteMovie.Movie;
            var tags = new List<string>();

            if (movie.Tags != null)
            {
                // Tags are stored as IDs, we pass them as-is for now
                // The external service can resolve them via the API if needed
                foreach (var tagId in movie.Tags)
                {
                    tags.Add(tagId.ToString());
                }
            }

            var movieResource = new ExternalHookMovieResource
            {
                Id = movie.Id,
                Title = movie.MovieMetadata.Value.Title,
                Year = movie.Year,
                FolderPath = movie.Path,
                TmdbId = movie.MovieMetadata.Value.TmdbId,
                ImdbId = movie.MovieMetadata.Value.ImdbId,
                QualityProfileName = movie.QualityProfile?.Name ?? "",
                QualityProfileId = movie.QualityProfileId,
                Tags = tags
            };

            var releases = new List<ExternalHookReleaseResource>();
            foreach (var decision in decisions)
            {
                var release = decision.RemoteMovie.Release;
                var torrentInfo = release as TorrentInfo;

                var releaseResource = new ExternalHookReleaseResource
                {
                    Guid = release.Guid,
                    Title = release.Title,
                    Indexer = release.Indexer,
                    IndexerId = release.IndexerId,
                    IndexerPriority = release.IndexerPriority,
                    Quality = decision.RemoteMovie.ParsedMovieInfo.Quality.Quality.Name,
                    QualityVersion = decision.RemoteMovie.ParsedMovieInfo.Quality.Revision.Version,
                    ReleaseGroup = decision.RemoteMovie.ParsedMovieInfo.ReleaseGroup,
                    CustomFormats = decision.RemoteMovie.CustomFormats?.ConvertAll(cf => cf.Name) ?? new List<string>(),
                    CustomFormatScore = decision.RemoteMovie.CustomFormatScore,
                    Size = release.Size,
                    Protocol = release.DownloadProtocol.ToString(),
                    Seeders = torrentInfo?.Seeders,
                    Leechers = torrentInfo?.Peers,
                    AgeMinutes = release.AgeMinutes,
                    PublishDate = release.PublishDate,
                    Languages = decision.RemoteMovie.Languages?.ConvertAll(l => l.Name) ?? new List<string>(),
                    IndexerFlags = BuildIndexerFlags(release.IndexerFlags),
                    IsRadarrTopPick = release.Guid == radarrTopPickGuid
                };

                releases.Add(releaseResource);
            }

            return new ExternalHookPayload
            {
                HookType = hookType,
                InstanceName = instanceName,
                ApplicationUrl = applicationUrl,
                Movie = movieResource,
                Releases = releases,
                RadarrTopPickGuid = radarrTopPickGuid
            };
        }

        private static List<string> BuildIndexerFlags(IndexerFlags flags)
        {
            var result = new List<string>();
            var flagValues = Enum.GetValues(typeof(IndexerFlags));

            foreach (IndexerFlags value in flagValues)
            {
                if (value != 0 && (flags & value) == value)
                {
                    result.Add(value.ToString());
                }
            }

            return result;
        }
    }
}
