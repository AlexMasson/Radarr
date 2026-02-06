# Radarr

> **⚡ This is a fork** with the **Download Decision Override** feature ([#11372](https://github.com/Radarr/Radarr/issues/11372)).
>
> Docker image: `ghcr.io/alexmasson/radarr:latest`
>
> Based on **stable** (`master`) · [📋 View all changes vs upstream](https://github.com/Radarr/Radarr/compare/master...AlexMasson:Radarr:feature/download_decision_override_stable)

---

## Why this fork?

Radarr's built-in release selection uses Custom Formats and quality scoring. It works, but expressing complex multi-criteria preferences — like "prefer MULTI with French dub, x265 for efficiency, avoid YIFY, 4-12GB for 1080p but allow remux up to 40GB for 4K" — requires dozens of CFs and scoring rules that are hard to reason about and maintain.

This fork adds a single feature: a **pre-grab webhook** that sends all candidate releases to an external service for evaluation. A plain-text prompt handles nuanced selection logic naturally. If the webhook fails or times out, Radarr falls back to its normal selection — nothing breaks.

The feature was proposed in [#11372](https://github.com/Radarr/Radarr/issues/11372) and rejected upstream. This fork implements it as a clean, minimal addition (~300 lines of C#) on top of the stable `master` branch.

## Fork Feature: Download Decision Override

### How it works

1. Radarr finds candidate releases for a movie
2. Before grabbing, it sends ALL candidates to a configured webhook URL
3. The webhook returns the GUID of the preferred release
4. Radarr downloads that specific release

**Fail-safe**: If the webhook fails, times out, or isn't configured — Radarr falls back to its normal selection.

### Settings UI

The configuration lives in **Settings → Download Clients → Download Decision Override**:

![Download Decision Override — Radarr](docs/screenshots/radarr-ddo-settings.png)

- **Enable**: Toggle the feature on/off
- **Webhook URL**: The endpoint that receives candidate releases
- **Timeout**: Max wait time before falling back to default selection (recommended: 30s)
- **Username/Password**: Optional Basic authentication

### Quick start

```yaml
# Use this fork instead of the official image
services:
  radarr:
    image: ghcr.io/alexmasson/radarr:latest
    # ... rest of your config stays the same
```

Then configure the webhook in **Settings → Download Clients → Download Decision Override**:
- URL: `http://your-webhook:8080/hook/radarr/override`
- Timeout: `30` seconds

### Related Projects

- **[arr-llm-release-picker](https://github.com/AlexMasson/arr-llm-release-picker)** — AI-powered release selection using LLMs (reference webhook implementation)
- **[AlexMasson/Sonarr](https://github.com/AlexMasson/Sonarr)** — Same feature for TV shows

---

[![Build Status](https://dev.azure.com/Radarr/Radarr/_apis/build/status/Radarr.Radarr?branchName=develop)](https://dev.azure.com/Radarr/Radarr/_build/latest?definitionId=1&branchName=develop)
[![Translation status](https://translate.servarr.com/widget/servarr/radarr/svg-badge.svg)](https://translate.servarr.com/engage/servarr/?utm_source=widget)
[![Docker Pulls](https://img.shields.io/docker/pulls/linuxserver/radarr.svg)](https://wiki.servarr.com/radarr/installation/docker)
![Github Downloads](https://img.shields.io/github/downloads/Radarr/Radarr/total.svg)
[![Backers on Open Collective](https://opencollective.com/Radarr/backers/badge.svg)](#backers)
[![Sponsors on Open Collective](https://opencollective.com/Radarr/sponsors/badge.svg)](#sponsors)
[![Mega Sponsors on Open Collective](https://opencollective.com/Radarr/megasponsors/badge.svg)](#mega-sponsors)

Radarr is a movie collection manager for Usenet and BitTorrent users. It can monitor multiple RSS feeds for new movies and will interface with clients and indexers to grab, sort, and rename them. It can also be configured to automatically upgrade the quality of existing files in the library when a better quality format becomes available.
Note that only one type of a given movie is supported. If you want both a 4k version and 1080p version of a given movie you will need multiple instances.

## Major Features Include

* Adding new movies with lots of information, such as trailers, ratings, etc.
* Support for major platforms: Windows, Linux, macOS, Raspberry Pi, etc.
* Can watch for better quality of the movies you have and do an automatic upgrade. _eg. from DVD to Blu-Ray_
* Automatic failed download handling will try another release if one fails
* Manual search so you can pick any release or to see why a release was not downloaded automatically
* Full integration with SABnzbd and NZBGet
* Automatically searching for releases as well as RSS Sync
* Automatically importing downloaded movies
* Recognizing Special Editions, Director's Cut, etc.
* Identifying releases with hardcoded subs
* Identifying releases with AKA movie names
* SABnzbd, NZBGet, QBittorrent, Deluge, rTorrent, Transmission, uTorrent, and other download clients are supported and integrated
* Full integration with Kodi and Plex (notifications, library updates)
* Importing Metadata such as trailers or subtitles
* Adding metadata such as posters and information for Kodi and others to use
* Advanced customization for profiles, such that Radarr will always download the copy you want
* A beautiful UI

## Support

[![Wiki](https://img.shields.io/badge/servarr-wiki-181717.svg?maxAge=60)](https://wiki.servarr.com/radarr)
[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://radarr.video/discord)

Note: GitHub Issues are for Bugs and Feature Requests Only

[![GitHub - Bugs and Feature Requests Only](https://img.shields.io/badge/github-issues-red.svg?maxAge=60)](https://github.com/Radarr/Radarr/issues)

## Contributors & Developers

[API Documentation](https://radarr.video/docs/api/)

This project exists thanks to all the people who contribute.
- [Contribute (GitHub)](CONTRIBUTING.md)
- [Contribution (Wiki Article)](https://wiki.servarr.com/radarr/contributing)

[![Contributors List](https://opencollective.com/Radarr/contributors.svg?width=890&button=false)](https://github.com/Radarr/Radarr/graphs/contributors)

## Backers

Thank you to all our backers! 🙏 [Become a backer](https://opencollective.com/Radarr#backer)

[![Backers List](https://opencollective.com/Radarr/backers.svg?width=890)](https://opencollective.com/Radarr#backer)

## Sponsors

Support this project by becoming a sponsor. Your logo will show up here with a link to your website. [Become a sponsor](https://opencollective.com/Radarr#sponsor)

[![Sponsors List](https://opencollective.com/Radarr/sponsors.svg?width=890)](https://opencollective.com/Radarr#sponsor)

## Mega Sponsors

[![Mega Sponsors List](https://opencollective.com/Radarr/tiers/mega-sponsor.svg?width=890)](https://opencollective.com/Radarr#mega-sponsor)

## JetBrains

Thank you to [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/jetbrains.png" alt="JetBrains" width="96">](http://www.jetbrains.com/) for providing us with free licenses to their great tools.

* [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/ReSharper_icon.png" alt="ReSharper" width="32"> ReSharper](http://www.jetbrains.com/resharper/)
* [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/WebStorm_icon.png" alt="WebStorm" width="32"> WebStorm](http://www.jetbrains.com/webstorm/)
* [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/Rider_icon.png" alt="Rider" width="32"> Rider](http://www.jetbrains.com/rider/)
* [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/dotTrace_icon.png" alt="dotTrace" width="32"> dotTrace](http://www.jetbrains.com/dottrace/)

## DigitalOcean

This project is also supported by DigitalOcean
<p>
  <a href="https://www.digitalocean.com/">
    <img src="https://opensource.nyc3.cdn.digitaloceanspaces.com/attribution/assets/SVG/DO_Logo_horizontal_blue.svg" width="201px">
  </a>
</p>

### License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2010-2025
