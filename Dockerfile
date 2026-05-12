# syntax=docker/dockerfile:1
# Local build of Radarr fork (feature/llm-prioritization).
# Build context = root of this repo.
# Used by /home/alexandre/docker/media/docker-compose.yaml

# --- Stage 1: Build frontend ---
FROM node:20-alpine AS frontend

WORKDIR /src
# Copier seulement les manifests d'abord → yarn install mis en cache tant que package.json/yarn.lock ne changent pas
COPY package.json yarn.lock /src/
RUN yarn install --frozen-lockfile

# Puis le reste du source (invalidé à chaque commit, mais yarn install reste caché)
COPY . /src
RUN yarn build --env production

# --- Stage 2: Build backend ---
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS builder

COPY --from=frontend /src /src

WORKDIR /src/src

RUN --mount=type=cache,id=radarr-nuget,target=/root/.nuget/packages \
    dotnet restore --disable-parallel Radarr.sln \
      -p:NuGetAudit=false \
      -p:TreatWarningsAsErrors=false && \
    dotnet msbuild Radarr.sln \
      -p:SelfContained=True \
      -p:Configuration=Release \
      -p:RuntimeIdentifiers=linux-musl-x64 \
      -t:PublishAllRids \
      -p:NuGetAudit=false \
      -p:TreatWarningsAsErrors=false && \
    mkdir /build && \
    cp -r /src/_output/net8.0/linux-musl-x64/publish/* /build/ && \
    cp -r /src/_output/UI /build/UI

# --- Stage 3: Runtime image (same as upstream linuxserver) ---
FROM ghcr.io/linuxserver/baseimage-alpine:3.23

ARG BUILD_DATE
ARG VERSION
LABEL build_version="Custom LLM-prioritization build:- ${VERSION} Build-date:- ${BUILD_DATE}"
LABEL maintainer="AlexMasson"

ENV XDG_CONFIG_HOME="/config/xdg" \
    COMPlus_EnableDiagnostics=0 \
    TMPDIR=/run/radarr-temp

RUN \
  echo "**** install packages ****" && \
  apk add -U --upgrade --no-cache \
    ffmpeg \
    icu-libs \
    sqlite-libs \
    xmlstarlet && \
  mkdir -p /app/radarr/bin

COPY --from=builder /build/ /app/radarr/bin/

# Ensure embedded ffprobe/ffmpeg binaries are executable for non-root user
RUN chmod a+rx /app/radarr/bin/ffprobe /app/radarr/bin/ffmpeg 2>/dev/null || true

RUN \
  echo -e "UpdateMethod=docker\nBranch=feature/llm-prioritization\nPackageVersion=${VERSION:-LocalBuild}\nPackageAuthor=AlexMasson (fork)" > /app/radarr/package_info && \
  printf "Custom build version: ${VERSION}\nBuild-date: ${BUILD_DATE}" > /build_version

# linuxserver s6-overlay services (copied from docker-radarr/root/)
COPY docker-root/ /

EXPOSE 7878

VOLUME /config
