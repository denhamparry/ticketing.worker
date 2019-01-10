# Ticketing.Worker

## Setup

### Local Environments

```bash
$ export ASPNETCORE_ENVIRONMENT=Development
```

### Docker

```bash
$ docker build -t denhamparry/ticketing.worker:local .
Sending build context to Docker daemon  612.9kB
Step 1/12 : FROM microsoft/dotnet:2.2-sdk AS build
 ---> a92c994e2c80
Step 2/12 : WORKDIR /app
 ---> Using cache
 ---> 6381d6ae2d33
Step 3/12 : COPY src/*.csproj ./src/
 ---> 08495ddb90f8
Step 4/12 : RUN dotnet restore src/
 ---> Running in 1b95987d915c
  Restoring packages for /app/src/ticketing.worker.csproj...
  Installing RabbitMQ.Client 5.1.0.
  Generating MSBuild file /app/src/obj/ticketing.worker.csproj.nuget.g.props.
  Generating MSBuild file /app/src/obj/ticketing.worker.csproj.nuget.g.targets.
  Restore completed in 1.46 sec for /app/src/ticketing.worker.csproj.
Removing intermediate container 1b95987d915c
 ---> dfb79b126f21
Step 5/12 : COPY src/. ./src/
 ---> 4d7f05562fea
Step 6/12 : WORKDIR /app/src
 ---> Running in fe2e5bd19cf3
Removing intermediate container fe2e5bd19cf3
 ---> c8b835c232e5
Step 7/12 : RUN dotnet publish -c Release -o out
 ---> Running in 4ddc67ec3b7b
Microsoft (R) Build Engine version 15.9.20+g88f5fadfbe for .NET Core
Copyright (C) Microsoft Corporation. All rights reserved.

  Restoring packages for /app/src/ticketing.worker.csproj...
  Generating MSBuild file /app/src/obj/ticketing.worker.csproj.nuget.g.props.
  Generating MSBuild file /app/src/obj/ticketing.worker.csproj.nuget.g.targets.
  Restore completed in 278.47 ms for /app/src/ticketing.worker.csproj.
  ticketing.worker -> /app/src/bin/Release/netcoreapp2.2/ticketing.worker.dll
  ticketing.worker -> /app/src/out/
Removing intermediate container 4ddc67ec3b7b
 ---> 8aeda20e9b7c
Step 8/12 : FROM microsoft/dotnet:2.2-aspnetcore-runtime AS runtime
 ---> 17ccc4e8f8af
Step 9/12 : WORKDIR /app
 ---> Using cache
 ---> 5a7d61c97f83
Step 10/12 : COPY --from=build /app/src/out ./
 ---> f62054153b6a
Step 11/12 : EXPOSE 80 443
 ---> Running in 89651e753009
Removing intermediate container 89651e753009
 ---> 4c62b58ee7c8
Step 12/12 : ENTRYPOINT ["dotnet", "ticketing.worker.dll"]
 ---> Running in 8838773931e2
Removing intermediate container 8838773931e2
 ---> 9c095d905c06
Successfully built 9c095d905c06
Successfully tagged denhamparry/ticketing.worker:local
```