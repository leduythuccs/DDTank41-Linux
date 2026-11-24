# DDTank game image: Center, Fighting, Road (game server), Tank.Web (website, Flash client, socket proxy) and
# GameAdmin.Web in one container. They talk to each other over 127.0.0.1, as with deploy/run-local.sh.
# Multi-arch: build it on the target machine (x64 or arm64) with deploy/docker/docker-compose.yml.
# Build context: the repository root (see /.dockerignore).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# amd64 or arm64, set by docker build: publish only that platform's native libraries (SkiaSharp...).
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN set -e; \
    rid="linux-$( [ "$TARGETARCH" = arm64 ] && echo arm64 || echo x64 )"; \
    for p in Center.Service Fighting.Service Road.Service Tank.Web GameAdmin.Web; do \
        dotnet publish "$p/$p.csproj" -c Release -r "$rid" --self-contained false -o "/app/$p" --nologo; \
    done; \
    find /app -name '*.pdb' -delete

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# fontconfig + Liberation fonts: SkiaSharp draws the wind numbers and captchas with "Arial" (Liberation Sans here).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fontconfig fonts-liberation fonts-dejavu-core tzdata \
    && rm -rf /var/lib/apt/lists/* \
    && fc-cache -f

# The servers write logs, records and compiled scripts next to their binaries: owned by "app".
COPY --from=build --chown=app:app /app /app
# Site content the servers read at runtime (owned by "app", so files checked out as 0600 stay readable).
COPY --chown=app:app ["Source Flash/FlashSV1", "/srv/ddtank/flash"]
COPY --chown=app:app Tank.Request /srv/ddtank/Tank.Request
COPY --chown=app:app Tank.Flash /srv/ddtank/Tank.Flash
COPY --chown=app:app GameAdmin /srv/ddtank/GameAdmin
COPY --chmod=755 deploy/docker/start.sh /usr/local/bin/ddtank-start

RUN mkdir -p /data/resource /data/resource-cache /var/log/ddtank \
    && chown app:app /data /data/resource /data/resource-cache /var/log/ddtank

# Workstation GC: server GC keeps a heap per CPU core and uses much more memory.
ENV DOTNET_gcServer=0 \
    TZ=Asia/Ho_Chi_Minh

EXPOSE 16200 16201
VOLUME ["/data/resource-cache", "/var/log/ddtank"]
# Starts as root to fix volume ownership, then runs the servers as the "app" user.
ENTRYPOINT ["/usr/local/bin/ddtank-start"]
