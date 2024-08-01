@echo off
if not exist "go.mod" (
    go mod init example.com/go-project
    go get github.com/heroiclabs/nakama-common/runtime@v1.26.0
)
go mod vendor
docker compose -p dojo-nakama-server up -d --build nakama
docker image prune
