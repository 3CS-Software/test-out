FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

RUN dotnet publish -c release --self-contained=true --runtime=win-x64 -p:PublishTrimmed=true -o /testout

# Modify this base image to your build requirements. See https://github.com/dotnet/dotnet-docker/tree/main/src/runtime-deps/6.0 for required libraries.
FROM mcr.microsoft.com/dotnet/runtime-deps:6.0

WORKDIR C:\\testout
COPY --from=build C:\\testout .

EXPOSE 34872
EXPOSE 34873

ENTRYPOINT [ "ThreeCS.TestOut.Console.exe" ]