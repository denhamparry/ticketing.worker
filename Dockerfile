FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /app

COPY src/console/*.csproj ./src/
RUN dotnet restore src/

COPY src/console/. ./src/
WORKDIR /app/src
RUN dotnet publish -c Release -o out


FROM microsoft/dotnet:2.2-aspnetcore-runtime AS runtime
WORKDIR /app
COPY --from=build /app/src/out ./
EXPOSE 80 443
ENTRYPOINT ["dotnet", "ticketing.worker.dll"]
