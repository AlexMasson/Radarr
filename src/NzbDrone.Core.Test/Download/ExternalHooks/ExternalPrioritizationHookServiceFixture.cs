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
    public class ExternalPrioritizationHookServiceFixture : CoreTest<ExternalPrioritizationHookService>
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
                .Setup(s => s.ExternalPrioritizationHookEnabled)
                .Returns(false);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalPrioritizationHookUrl)
                .Returns("http://localhost:8080/hook/prioritize");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalPrioritizationHookTimeout)
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
                .Setup(s => s.ExternalPrioritizationHookEnabled)
                .Returns(true);
        }

        private void GivenWebhookReturns(ExternalPrioritizationHookResponse response)
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

        [Test]
        public async Task should_proceed_when_disabled()
        {
            var result = await Subject.EvaluateAsync(_decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().HaveCount(3);
            result.ModifiedDecisions.Select(d => d.RemoteMovie.Release.Guid)
                .Should().ContainInOrder("guid-1", "guid-2", "guid-3");

            Mocker.GetMock<IHttpClient>()
                .Verify(s => s.ExecuteAsync(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task should_proceed_when_url_empty()
        {
            GivenHookEnabled();

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ExternalPrioritizationHookUrl)
                .Returns("");

            var result = await Subject.EvaluateAsync(_decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().HaveCount(3);

            Mocker.GetMock<IHttpClient>()
                .Verify(s => s.ExecuteAsync(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task should_select_specific_release()
        {
            GivenHookEnabled();

            GivenWebhookReturns(new ExternalPrioritizationHookResponse
            {
                SelectedGuid = "guid-2"
            });

            var result = await Subject.EvaluateAsync(_decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().HaveCount(3);
            result.ModifiedDecisions[0].RemoteMovie.Release.Guid.Should().Be("guid-2");
        }

        [Test]
        public async Task should_reorder_releases()
        {
            GivenHookEnabled();

            GivenWebhookReturns(new ExternalPrioritizationHookResponse
            {
                OrderedGuids = new List<string> { "guid-3", "guid-1", "guid-2" }
            });

            var result = await Subject.EvaluateAsync(_decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().HaveCount(3);
            result.ModifiedDecisions.Select(d => d.RemoteMovie.Release.Guid)
                .Should().ContainInOrder("guid-3", "guid-1", "guid-2");
        }

        [Test]
        public async Task should_proceed_on_http_error()
        {
            GivenHookEnabled();
            GivenWebhookThrows();

            var result = await Subject.EvaluateAsync(_decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().HaveCount(3);
            result.ModifiedDecisions.Select(d => d.RemoteMovie.Release.Guid)
                .Should().ContainInOrder("guid-1", "guid-2", "guid-3");

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_defer_when_requested()
        {
            GivenHookEnabled();

            GivenWebhookReturns(new ExternalPrioritizationHookResponse
            {
                Defer = true,
                DeferMinutes = 60,
                Reason = "Waiting for better release"
            });

            var result = await Subject.EvaluateAsync(_decisions);

            result.ShouldProceed.Should().BeFalse();
            result.DeferMinutes.Should().Be(60);
            result.Reason.Should().Contain("Waiting for better release");
        }

        [Test]
        public async Task should_proceed_when_selected_guid_not_found()
        {
            GivenHookEnabled();

            GivenWebhookReturns(new ExternalPrioritizationHookResponse
            {
                SelectedGuid = "guid-unknown"
            });

            var result = await Subject.EvaluateAsync(_decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().HaveCount(3);
            result.ModifiedDecisions.Select(d => d.RemoteMovie.Release.Guid)
                .Should().ContainInOrder("guid-1", "guid-2", "guid-3");
        }

        [Test]
        public async Task should_proceed_on_empty_response()
        {
            GivenHookEnabled();

            GivenWebhookReturns(new ExternalPrioritizationHookResponse());

            var result = await Subject.EvaluateAsync(_decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().HaveCount(3);
            result.ModifiedDecisions.Select(d => d.RemoteMovie.Release.Guid)
                .Should().ContainInOrder("guid-1", "guid-2", "guid-3");
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

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().HaveCount(3);

            Mocker.GetMock<IHttpClient>()
                .Verify(s => s.ExecuteAsync(It.IsAny<HttpRequest>()), Times.Never());
        }
    }
}
