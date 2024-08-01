package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"math/rand"
	"net/http"

	"github.com/heroiclabs/nakama-common/runtime"
)

// RPC: JoinOrNewMatch
// Query active matches, join or request a new match from Instance Server

type RPCJoinOrNewMatchPayload struct {
	GameTag       string `json:"GameTag"`
	MaxNumPlayers int    `json:"MaxNumPlayers"`
}

func rpcJoinOrNewMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	prop := RPCJoinOrNewMatchPayload{}

	if err := json.Unmarshal([]byte(payload), &prop); err != nil {
		logger.Error("[rpcJoinOrNewMatch] received invalid payload = %s", payload)
	} else {
		queryParams := MatchStorageQueryRequestParams{
			GameTag:       prop.GameTag,
			ServerId:      "", // empty to query all
			MaxNumPlayers: prop.MaxNumPlayers,
			MaxNumRecords: 1,
		}
		if matches, err := queryMatchStorage(queryParams, ctx, logger, nk); err != nil || len(matches) == 0 {
			http.Head(fmt.Sprintf("http://host.docker.internal:9000/nakama?query=%s", prop.GameTag))
			logger.Info("[rpcJoinOrNewMatch] no match found, sending new instance request")
		} else {
			match := matches[rand.Intn(len(matches))]
			storageData := MatchStorageData{}
			if err := json.Unmarshal([]byte(match.Payload), &storageData); err != nil {
				logger.Error("[rpcJoinOrNewMatch] received invalid storage payload = %s", match.Payload)
			} else {
				return storageData.MatchId, nil
			}
		}
	}
	return "", nil
}

// RPC: RemoveUserAccount
// Remove user account from Nakama, for cleanup

type RPCRemoveUserAccountPayload struct {
	UserId string `json:"UserId"`
}

func rpcRemoveUserAccount(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	player := RPCRemoveUserAccountPayload{}

	if err := json.Unmarshal([]byte(payload), &player); err != nil {
		logger.Error("[rpcRemoveUserAccount] received invalid payload = %s", payload)
	} else if err := nk.AccountDeleteId(ctx, player.UserId, false); err == nil {
		logger.Info(`[rpcRemoveUserAccount] removed user (%s)`, player.UserId)
	} else {
		logger.Error(`[rpcRemoveUserAccount] failed to remove user (%s)`, player.UserId)
	}
	return "", nil
}

// RPC: CreateMatch
// Create new match and custom info in storage

type RPCCreateMatchPayload struct {
	ServerId      string `json:"ServerId"`
	MatchId       string `json:"MatchId"`
	GameTag       string `json:"GameTag"`
	MaxNumPlayers int    `json:"MaxNumPlayers"`
}

func rpcCreateMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	prop := RPCCreateMatchPayload{}

	if err := json.Unmarshal([]byte(payload), &prop); err != nil {
		logger.Error("[rpcCreateMatch] received invalid payload = %s", payload)
	} else {
		// make sure match exists
		if _, err := nk.MatchGet(ctx, prop.MatchId); err != nil {
			logger.Error("[rpcCreateMatch] failed to find match = %s", prop.MatchId)
		} else {
			// register in storage
			initParams := MatchStorageInitParams{
				GameTag:       prop.GameTag,
				ServerId:      prop.ServerId,
				MatchId:       prop.MatchId,
				MaxNumPlayers: prop.MaxNumPlayers,
			}
			if err := initMatchStorage(initParams, ctx, logger, nk); err != nil {
				logger.Error("[rpcCreateMatch] failed")
			} else {
				return "success", nil
			}
		}
	}
	return "", nil
}

// RPC: UpdateMatch
// Update info stored with match storage

type RPCUpdateMatchPayload struct {
	ServerId string `json:"ServerId"`
	GameTag  string `json:"GameTag"`
	// data to update
	NumPlayers int `json:"NumPlayers"`
	NumClients int `json:"NumClients"`
}

func rpcUpdateMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	prop := RPCUpdateMatchPayload{}

	if err := json.Unmarshal([]byte(payload), &prop); err != nil {
		logger.Error("[rpcUpdateMatch] received invalid payload = %s", payload)
	} else {
		// update storage
		updateParams := MatchStorageUpdateParams{
			GameTag:    prop.GameTag,
			ServerId:   prop.ServerId,
			NumPlayers: prop.NumPlayers,
			NumClients: prop.NumClients,
		}
		if err := updateMatchStorage(updateParams, ctx, logger, nk); err != nil {
			logger.Error("[rpcUpdateMatch] failed")
		} else {
			return "success", nil
		}
	}
	return "", nil
}

// RPC: CleanMatch
// Clean up match storage

type RPCCleanMatchPayload struct {
	ServerId string `json:"ServerId"`
	GameTag  string `json:"GameTag"`
}

func rpcCleanMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	prop := RPCCleanMatchPayload{}

	if err := json.Unmarshal([]byte(payload), &prop); err != nil {
		logger.Error("[rpcRemoveMatch] received invalid payload = %s", payload)
	} else {
		// clean up storage
		cleanParams := MatchStorageCleanParams{
			GameTag:  prop.GameTag,
			ServerId: prop.ServerId,
		}
		if err := cleanMatchStorage(cleanParams, ctx, logger, nk); err != nil {
			logger.Error("[rpcRemoveMatch] failed")
		} else {
			return "success", nil
		}
	}
	return "", nil
}

// RPC: QueryMatch
// Query an active match and get its info, return json-encoded "MatchStorageData"

type RPCQueryMatchPayload struct {
	GameTag  string `json:"GameTag"`
	ServerId string `json:"ServerId"`
}

func rpcQueryMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	prop := RPCQueryMatchPayload{}

	if err := json.Unmarshal([]byte(payload), &prop); err != nil {
		logger.Error("[rpcQueryMatch] received invalid payload = %s", payload)
	} else {
		// query storage
		queryParams := MatchStorageQueryRequestParams{
			GameTag:       prop.GameTag,
			ServerId:      prop.ServerId,
			MaxNumPlayers: -1,
			MaxNumRecords: 1,
		}
		if records, err := queryMatchStorage(queryParams, ctx, logger, nk); err != nil || len(records) == 0 {
			logger.Error("[rpcQueryMatch] failed")
		} else {
			record := records[0]
			return record.Payload, nil
		}
	}
	return "", nil
}

// RPC: QueryMatches
// Query all active matches, return json-encoded list of json-encoded "MatchStorageData"

type RPCQueryMatchesPayload struct {
	GameTag       string `json:"GameTag"`
	MaxNumPlayers int    `json:"MaxNumPlayers"`
	MaxNumRecords int    `json:"MaxNumRecords"`
}

func rpcQueryMatches(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	prop := RPCQueryMatchesPayload{}
	response := []string{}

	if err := json.Unmarshal([]byte(payload), &prop); err != nil {
		logger.Error("[rpcQueryMatch] received invalid payload = %s", payload)
	} else {
		// query storage
		queryParams := MatchStorageQueryRequestParams{
			GameTag:       prop.GameTag,
			ServerId:      "",
			MaxNumPlayers: prop.MaxNumPlayers,
			MaxNumRecords: prop.MaxNumRecords,
		}
		if records, err := queryMatchStorage(queryParams, ctx, logger, nk); err != nil || len(records) == 0 {
			logger.Error("[rpcQueryMatch] failed")
		} else {
			for _, record := range records {
				response = append(response, record.Payload)
			}
			if payload, err := json.Marshal(response); err != nil {
				logger.Error("[rpcQueryMatch] invalid payload json format")
			} else {
				return string(payload), nil
			}
		}
	}
	return "", nil
}
