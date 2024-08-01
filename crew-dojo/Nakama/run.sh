if [ ! -f "go.mod" ]; then
    go mod init example.com/go-project
    go get github.com/heroiclabs/nakama-common/runtime@v1.26.0
fi
go mod vendor
docker compose -p dojo-nakama-server up -d --build nakama
docker image prune
