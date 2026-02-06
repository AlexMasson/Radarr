using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.DownloadDecisionOverride;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.DownloadDecisionOverride
{
    [TestFixture]
    public class DownloadDecisionOverrideServiceFixture : CoreTest<DownloadDecisionOverrideService>
    {
        private Movie _movie;
        private List<DownloadDecision> _decisions;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                .With(m => m.Id = 1)
                .With(m => m.Title = "Test Movie")
                .With(m => m.Year = 2024)
                .With(m => m.TmdbId = 12345)
                .With(m => m.ImdbId = "tt1234567")
                .Build();

            _decisions = new List<DownloadDecision>
            {
                CreateDecision("Release 1", "guid-1"),
                CreateDecision("Release 2", "guid-2"),
                CreateDecision("Release 3", "guid-3")
            };

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverrideEnabled)
                .Returns(false);

            Mocker.GetMock<IConfigFileProvider>()
                .Setup(s => s.InstanceName)
                .Returns("Radarr");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ApplicationUrl)
                .Returns("http://localhost:7878");
        }

        private DownloadDecision CreateDecision(string title, string guid)
        {
            var remoteMovie = new RemoteMovie
            {
                Movie = _movie,
                Release = new ReleaseInfo
                {
                    Title = title,
                    Guid = guid,
                    Size = 1000000000,
                    DownloadProtocol = DownloadProtocol.Torrent,
                    Indexer = "TestIndexer",
                    IndexerId = 1
                },
                ParsedMovieInfo = new ParsedMovieInfo
                {
                    Quality = new QualityModel(Quality.HDTV720p)
                },
                CustomFormats = new List<CustomFormat>(),
                CustomFormatScore = 0
            };

            return new DownloadDecision(remoteMovie);
        }

        [Test]
        public async Task should_proceed_when_disabled()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverrideEnabled)
                .Returns(false);

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().BeEquivalentTo(_decisions);

            Mocker.GetMock<IHttpClient>()
                .Verify(c => c.ExecuteAsync(It.IsAny<HttpRequest>()), Times.Never);
        }

        [Test]
        public async Task should_proceed_when_url_not_configured()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverrideEnabled)
                .Returns(true);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverrideUrl)
                .Returns(string.Empty);

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().BeEquivalentTo(_decisions);
        }

        [Test]
        public async Task should_proceed_when_approved()
        {
            SetupEnabledOverride();
            SetupWebhookResponse(new DownloadDecisionOverrideResponse { Approved = true });

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().BeEquivalentTo(_decisions);
        }

        [Test]
        public async Task should_reject_when_not_approved()
        {
            SetupEnabledOverride();
            SetupWebhookResponse(new DownloadDecisionOverrideResponse
            {
                Approved = false,
                Reason = "LLM says no"
            });

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeFalse();
            result.RejectionReason.Should().Be("LLM says no");
        }

        [Test]
        public async Task should_defer_when_defer_minutes_specified()
        {
            SetupEnabledOverride();
            SetupWebhookResponse(new DownloadDecisionOverrideResponse
            {
                Approved = false,
                DeferMinutes = 60,
                Reason = "Wait for better release"
            });

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeFalse();
            result.DeferMinutes.Should().Be(60);
            result.RejectionReason.Should().Be("Wait for better release");
        }

        [Test]
        public async Task should_select_specific_release()
        {
            SetupEnabledOverride();
            SetupWebhookResponse(new DownloadDecisionOverrideResponse
            {
                Approved = true,
                SelectedReleaseGuid = "guid-2"
            });

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions[0].RemoteMovie.Release.Guid.Should().Be("guid-2");
            result.ModifiedDecisions.Should().HaveCount(3);
        }

        [Test]
        public async Task should_reorder_releases()
        {
            SetupEnabledOverride();
            SetupWebhookResponse(new DownloadDecisionOverrideResponse
            {
                Approved = true,
                ReleaseOrder = new List<string> { "guid-3", "guid-1", "guid-2" }
            });

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions[0].RemoteMovie.Release.Guid.Should().Be("guid-3");
            result.ModifiedDecisions[1].RemoteMovie.Release.Guid.Should().Be("guid-1");
            result.ModifiedDecisions[2].RemoteMovie.Release.Guid.Should().Be("guid-2");
        }

        [Test]
        public async Task should_proceed_on_http_error()
        {
            SetupEnabledOverride();

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.ExecuteAsync(It.IsAny<HttpRequest>()))
                .ThrowsAsync(new HttpException(new HttpRequest("http://test"), new HttpResponse(new HttpRequest("http://test"), new HttpHeader(), "", HttpStatusCode.InternalServerError)));

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().BeEquivalentTo(_decisions);

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_proceed_on_timeout()
        {
            SetupEnabledOverride();

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.ExecuteAsync(It.IsAny<HttpRequest>()))
                .ThrowsAsync(new System.OperationCanceledException());

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().BeEquivalentTo(_decisions);

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_use_default_when_selected_guid_not_found()
        {
            SetupEnabledOverride();
            SetupWebhookResponse(new DownloadDecisionOverrideResponse
            {
                Approved = true,
                SelectedReleaseGuid = "nonexistent-guid"
            });

            var result = await Subject.EvaluateAsync(_movie, _decisions);

            result.ShouldProceed.Should().BeTrue();
            result.ModifiedDecisions.Should().BeEquivalentTo(_decisions);

            ExceptionVerification.ExpectedWarns(1);
        }

        private void SetupEnabledOverride()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverrideEnabled)
                .Returns(true);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverrideUrl)
                .Returns("http://localhost:5000/override");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverrideTimeout)
                .Returns(30);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverrideUsername)
                .Returns(string.Empty);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadDecisionOverridePassword)
                .Returns(string.Empty);
        }

        private void SetupWebhookResponse(DownloadDecisionOverrideResponse response)
        {
            var httpResponse = new HttpResponse(
                new HttpRequest("http://test"),
                new HttpHeader(),
                response.ToJson(),
                HttpStatusCode.OK);

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.ExecuteAsync(It.IsAny<HttpRequest>()))
                .ReturnsAsync(httpResponse);
        }
    }
}
