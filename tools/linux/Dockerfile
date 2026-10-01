# Build environment for the Linux AppImage (tools/linux/build-appimage.sh), so no local .NET SDK is needed.
#
#   docker build -t kyr-linux tools/linux
#   docker run --rm --user "$(id -u):$(id -g)" -v "$PWD:/src" -v "/path/to/your/disc:/disc:ro" \
#       kyr-linux /disc/YourDisc.cue
#
# The repository is mounted at /src (clone it with --recurse-submodules first); the AppImage lands in dist/.
FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
 && apt-get install -y --no-install-recommends git curl python3 file ca-certificates \
 && rm -rf /var/lib/apt/lists/* \
 && git config --system --add safe.directory '*'

# writable homes for dotnet/NuGet when running as the host user
ENV HOME=/tmp/home \
    DOTNET_CLI_HOME=/tmp/home \
    NUGET_PACKAGES=/tmp/home/.nuget/packages \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
RUN mkdir -p /tmp/home && chmod 1777 /tmp/home

WORKDIR /src
ENTRYPOINT ["bash", "tools/linux/build-appimage.sh"]
