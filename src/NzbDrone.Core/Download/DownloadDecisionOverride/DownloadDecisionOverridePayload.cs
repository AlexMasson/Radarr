using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download.DownloadDecisionOverride
{
    public class DownloadDecisionOverridePayload
    {
        public string EventType => "DownloadDecisionOverride";
        public string InstanceName { get; set; }
        public string ApplicationUrl { get; set; }
        public DownloadDecisionOverrideMovieResource Movie { get; set; }
        public List<DownloadDecisionOverrideReleaseResource> Releases { get; set; }
    }

    public class DownloadDecisionOverrideMovieResource
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Year { get; set; }
        public string FolderPath { get; set; }
        public int TmdbId { get; set; }
        public string ImdbId { get; set; }
    }

    public class DownloadDecisionOverrideReleaseResource
    {
        public DownloadDecisionOverrideReleaseResource()
        {
        }

        public DownloadDecisionOverrideReleaseResource(RemoteMovie remoteMovie, bool isSelected)
        {
            var release = remoteMovie.Release;
            var parsedInfo = remoteMovie.ParsedMovieInfo;

            Guid = release.Guid;
            Title = release.Title;
            Indexer = release.Indexer;
            IndexerId = release.IndexerId;
            IndexerPriority = release.IndexerPriority;
            Size = release.Size;
            Protocol = release.DownloadProtocol.ToString();
            AgeMinutes = release.AgeMinutes;
            PublishDate = release.PublishDate;
            IsSelected = isSelected;

            if (parsedInfo != null)
            {
                Quality = parsedInfo.Quality?.Quality?.Name;
                QualityVersion = parsedInfo.Quality?.Revision?.Version ?? 1;
                ReleaseGroup = parsedInfo.ReleaseGroup;
            }

            CustomFormats = remoteMovie.CustomFormats?.Select(cf => cf.Name).ToList() ?? new List<string>();
            CustomFormatScore = remoteMovie.CustomFormatScore;
            Languages = remoteMovie.Languages?.Select(l => l.Name).ToList() ?? new List<string>();

            IndexerFlags = Enum.GetValues(typeof(Parser.Model.IndexerFlags))
                .Cast<Parser.Model.IndexerFlags>()
                .Where(f => (release.IndexerFlags & f) == f)
                .Select(f => f.ToString())
                .ToList();

            // Handle TorrentInfo-specific properties
            if (release is TorrentInfo torrentInfo)
            {
                Seeders = torrentInfo.Seeders;
                Leechers = torrentInfo.Peers.HasValue && torrentInfo.Seeders.HasValue
                    ? torrentInfo.Peers.Value - torrentInfo.Seeders.Value
                    : null;
            }
        }

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
        public DateTime PublishDate { get; set; }
        public List<string> Languages { get; set; }
        public List<string> IndexerFlags { get; set; }
        public bool IsSelected { get; set; }
    }
}
