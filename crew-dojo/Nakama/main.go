package main

import (
	"context"
	"database/sql"

	"github.com/heroiclabs/nakama-common/runtime"
)

func main() {}

func RegisterRpc(logger runtime.Logger, initializer runtime.Initializer, rpcName string, rpcFunc func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error)) error {
	if err := initializer.RegisterRpc(rpcName, rpcFunc); err != nil {
		logger.Info("Failed to register RPC (%s)", rpcName)
		return err
	} else {
		logger.Info("RPC (%s) registered successfully", rpcName)
	}
	return nil
}

func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	if err := RegisterRpc(logger, initializer, "RPCJoinOrNewMatch", rpcJoinOrNewMatch); err != nil {
		return err
	}
	if err := RegisterRpc(logger, initializer, "RPCRemoveUserAccount", rpcRemoveUserAccount); err != nil {
		return err
	}
	if err := RegisterRpc(logger, initializer, "RPCCreateMatch", rpcCreateMatch); err != nil {
		return err
	}
	if err := RegisterRpc(logger, initializer, "RPCUpdateMatch", rpcUpdateMatch); err != nil {
		return err
	}
	if err := RegisterRpc(logger, initializer, "RPCCleanMatch", rpcCleanMatch); err != nil {
		return err
	}
	if err := RegisterRpc(logger, initializer, "RPCQueryMatch", rpcQueryMatch); err != nil {
		return err
	}
	if err := RegisterRpc(logger, initializer, "RPCQueryMatches", rpcQueryMatches); err != nil {
		return err
	}
	return nil
}
