FROM mcr.microsoft.com/dotnet/sdk:6.0 AS pkg-restore

WORKDIR /build

COPY ThreeCS.TestOut.sln \
     **/*.csproj \
     ./
# Docker wildcard copies all files into a flat structure. This will put csproj files in their supposed locations
# Another approach is described here: https://github.com/benmccallum/dotnet-references/blob/master/docs/Dockerfile-use-case.md
RUN for %f in (*.csproj) do (mkdir %~nf && move %f %~nf)

RUN dotnet restore --packages packages --runtime=win-x64
RUN xcopy packages\nunit\3.13.2 packages\NUnit.3.13.2\ /S

FROM pkg-restore AS build

WORKDIR /build
COPY . .

RUN dotnet publish --no-restore --configuration release --self-contained=true --framework=net6.0 --runtime=win-x64 -p:PublishTrimmed=true -o /testout

# Modify this base image to your build requirements.
FROM mcr.microsoft.com/windows/nanoserver:1809

WORKDIR C:\\testout
COPY --from=build C:\\testout .

EXPOSE 34872
EXPOSE 34873

ENTRYPOINT [ "ThreeCS.TestOut.Console.exe" ]