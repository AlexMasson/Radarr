using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.ExternalHooks;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.ExternalHooks
{
    [TestFixture]
    public class ExternalRejectionHookServiceFixture : CoreTest<ExternalRejectionHookService>
    {
        private List<DownloadDecision> _decisions;
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            var movieMetadata = new MovieMetadata
            {
                Title = "Test Movie",
                TmdbId = 12345,
                ImdbId = "tt1234567",
                OriginalLanguage = Language.English
            };

            _movie = new Movie
            {
                Id = 1,
                Year = 2024,
                Path = "/movies/Test Movie (2024)",
                QualityProfileId = 1,
                QualityProfile = new QualityProfile { Name = "HD-1080p" },
                MovieMetadata = movieMetadata,
                Tags = new HashSet<int>()
            };

            _decisions = new List<DownloadDecision>
            {
                CreateDecision("guid-1", "Release.1080p.x264-GROUP1"),
                CreateDecision("guid-2", "Release.1080p.x265-GROUP2"),
                CreateDecision("guid-3", "Release.720p.x264-GROUP3")
            };

            // Default: disabled
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalRejectionHookEnabled)
                .Returns(false);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalRejectionHookUrl)
                .Returns("http://localhost:8080/hook/reject");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalRejectionHookTimeout)
                .Returns(10);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalHooksUsername)
                .Returns("");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalHooksPassword)
                .Returns("");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalHooksSkipTag)
                .Returns("");

            Mocker.GetMock<IConfigFileProvider>()
                .Setup(s => s.InstanceName)
                .Returns("Radarr");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ApplicationUrl)
                .Returns("http://localhost:7878");
        }

        private DownloadDecision CreateDecision(string guid, string title)
        {
            var quality = new QualityModel(Quality.Bluray1080p, new Revision(version: 1));

            var release = new ReleaseInfo
            {
                Guid = guid,
                Title = title,
                Size = 1500000000,
                Indexer = "TestIndexer",
                IndexerId = 1,
                IndexerPriority = 25,
                DownloadProtocol = DownloadProtocol.Torrent,
                PublishDate = DateTime.UtcNow.AddHours(-2),
                Languages = new List<Language> { Language.English }
            };

            var remoteMovie = new RemoteMovie
            {
                Release = release,
                ParsedMovieInfo = new ParsedMovieInfo { Quality = quality, ReleaseGroup = "GROUP" },
                Movie = _movie,
                CustomFormats = new List<CustomFormat>(),
                CustomFormatScore = 0,
                Languages = new List<Language> { Language.English }
            };

            return new DownloadDecision(remoteMovie);
        }

        private void GivenHookEnabled()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalRejectionHookEnabled)
                .Returns(true);
        }

        private void GivenWebhookReturns(ExternalRejectionHookResponse response)
        {
            var json = response.ToJson();

            Mocker.GetMock<IHttpClient>()
                .Setup(s => s.ExecuteAsync(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r => Task.FromResult(
                    new HttpResponse(r, new HttpHeader(), json, HttpStatusCode.OK)));
        }

        private void GivenWebhookThrows()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(s => s.ExecuteAsync(It.IsAny<HttpRequest>()))
                .ThrowsAsync(new WebException("Connection refused"));
        }

        private void GivenWebhookTimesOut()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(s => s.ExecuteAsync(It.IsAny<HttpRequest>()))
                .ThrowsAsync(new TaskCanceledException("The request timed out"));
        }

        [Test]
        public async Task should_return_all_releases_when_disabled()
        {
            var result = await Subject.EvaluateAsync(_decisions);

            result.Accepted.Should().HaveCount(3);
            result.Rejected.Should().BeEmpty();

            Mocker.GetMock<IHttpClient>()
                .Verify(s => s.ExecuteAsync(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task should_return_all_releases_when_url_empty()
        {
            GivenHookEnabled();

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalRejectionHookUrl)
                .Returns("");

            var result = await Subject.EvaluateAsync(_decisions);

            result.Accepted.Should().HaveCount(3);
            result.Rejected.Should().BeEmpty();

            Mocker.GetMock<IHttpClient>()
                .Verify(s => s.ExecuteAsync(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task should_reject_releases_marked_by_webhook()
        {
            GivenHookEnabled();

            GivenWebhookReturns(new ExternalRejectionHookResponse
            {
                Decisions = new List<ExternalRejectionDecision>
                {
                    new ExternalRejectionDecision { Guid = "guid-1", Rejected = true, Reason = "bad quality" }
                }
            });

            var result = await Subject.EvaluateAsync(_decisions);

            result.Accepted.Should().HaveCount(2);
            result.Rejected.Should().HaveCount(1);
            result.Rejected[0].RemoteMovie.Release.Guid.Should().Be("guid-1");
            result.Rejected[0].Rejections.First().Message.Should().Contain("bad quality");
        }

        [Test]
        public async Task should_keep_releases_not_rejected()
        {
            GivenHookEnabled();

            GivenWebhookReturns(new ExternalRejectionHookResponse
            {
                Decisions = new List<ExternalRejectionDecision>
                {
                    new ExternalRejectionDecision { Guid = "guid-1", Rejected = true, Reason = "bad" },
                    new ExternalRejectionDecision { Guid = "guid-2", Rejected = false }
                }
            });

            var result = await Subject.EvaluateAsync(_decisions);

            result.Accepted.Should().HaveCount(2);
            result.Accepted.Select(d => d.RemoteMovie.Release.Guid)
                .Should().Contain("guid-2")
                .And.Contain("guid-3");
            result.Rejected.Should().HaveCount(1);
            result.Rejected[0].RemoteMovie.Release.Guid.Should().Be("guid-1");
        }

        [Test]
        public async Task should_return_all_releases_on_http_error()
        {
            GivenHookEnabled();
            GivenWebhookThrows();

            var result = await Subject.EvaluateAsync(_decisions);

            result.Accepted.Should().HaveCount(3);
            result.Rejected.Should().BeEmpty();

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_return_all_releases_on_timeout()
        {
            GivenHookEnabled();
            GivenWebhookTimesOut();

            var result = await Subject.EvaluateAsync(_decisions);

            result.Accepted.Should().HaveCount(3);
            result.Rejected.Should().BeEmpty();

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_skip_when_movie_has_skip_tag()
        {
            GivenHookEnabled();

            _movie.Tags = new HashSet<int> { 42 };

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalHooksSkipTag)
                .Returns("42");

            var result = await Subject.EvaluateAsync(_decisions);

            result.Accepted.Should().HaveCount(3);
            result.Rejected.Should().BeEmpty();

            Mocker.GetMock<IHttpClient>()
                .Verify(s => s.ExecuteAsync(It.IsAny<HttpRequest>()), Times.Never());
        }
    }
}
