FROM debian:bookworm-slim

ARG DOTNET_SDK_CHANNEL=10.0
ARG DOTNET_RUNTIME_CHANNELS="9.0 8.0 6.0"
ARG GO_VERSION=1.26.3
ARG NODE_VERSION=22.12.0
ARG YARN_VERSION=1.22.21
ARG FOUNDRY_VERSION=v1.3.0
ARG PWSH_VERSION=7.4.6
ARG UV_VERSION=0.8.22
ARG PDM_VERSION=2.25.4

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl git unzip xz-utils build-essential \
      python3 python3-venv python3-pip \
      libjemalloc2 libsnappy1v5 libicu72 libssl3 zlib1g procps \
 && rm -rf /var/lib/apt/lists/*

ENV DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    PATH=/usr/share/dotnet:/usr/local/go/bin:/usr/local/node/bin:/usr/local/foundry:$PATH

RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel "$DOTNET_SDK_CHANNEL" --install-dir /usr/share/dotnet \
 && for channel in $DOTNET_RUNTIME_CHANNELS; do \
      bash /tmp/dotnet-install.sh --channel "$channel" --runtime dotnet --install-dir /usr/share/dotnet; \
      bash /tmp/dotnet-install.sh --channel "$channel" --runtime aspnetcore --install-dir /usr/share/dotnet; \
    done \
 && rm /tmp/dotnet-install.sh

RUN curl -sSL "https://go.dev/dl/go${GO_VERSION}.linux-amd64.tar.gz" | tar -xz -C /usr/local

RUN mkdir -p /usr/local/node \
 && curl -sSL "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.xz" \
    | tar -xJ -C /usr/local/node --strip-components=1 \
 && npm install -g "yarn@${YARN_VERSION}"

RUN mkdir -p /usr/local/foundry \
 && curl -sSL "https://github.com/foundry-rs/foundry/releases/download/${FOUNDRY_VERSION}/foundry_${FOUNDRY_VERSION}_linux_amd64.tar.gz" \
    | tar -xz -C /usr/local/foundry

RUN mkdir -p /usr/local/pwsh \
 && curl -sSL "https://github.com/PowerShell/PowerShell/releases/download/v${PWSH_VERSION}/powershell-${PWSH_VERSION}-linux-x64.tar.gz" \
    | tar -xz -C /usr/local/pwsh \
 && chmod +x /usr/local/pwsh/pwsh \
 && ln -s /usr/local/pwsh/pwsh /usr/local/bin/pwsh

ENV UV_PYTHON_INSTALL_DIR=/opt/uv/python \
    UV_TOOL_DIR=/opt/uv/tools \
    UV_TOOL_BIN_DIR=/usr/local/bin
RUN curl -LsSf "https://astral.sh/uv/${UV_VERSION}/install.sh" | env UV_INSTALL_DIR=/usr/local/bin UV_NO_MODIFY_PATH=1 sh \
 && uv python install 3.10 \
 && uv tool install "pdm==${PDM_VERSION}" --with "hishel<1" \
 && chmod -R a+rX /opt/uv

ENV HOME=/home/ci \
    GOPATH=/home/ci/go \
    GOCACHE=/home/ci/.cache/go-build
RUN mkdir -p /home/ci/.nuget/packages /home/ci/go /home/ci/.cache \
 && chmod -R 1777 /home/ci

WORKDIR /src
