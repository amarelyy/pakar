FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY src/Pakar.Shared/ src/Pakar.Shared/
COPY src/Pakar.Vision/ src/Pakar.Vision/
COPY src/Pakar.AiInference/ src/Pakar.AiInference/
RUN dotnet publish src/Pakar.AiInference -c Release -r linux-x64 --self-contained false -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
# The full OpenCvSharp native runtime includes videoio/FFmpeg and requires GTK3.
RUN apt-get update && apt-get install -y --no-install-recommends libgtk-3-0t64 libgomp1 libdrm2 libatomic1 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080 Model__Path=/models/best.onnx Worker__VideoRoot=/videos
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pakar.AiInference.dll"]
