package main

import (
	"context"
	"encoding/json"
	"errors"

	"github.com/heroiclabs/nakama-common/api"
	"github.com/heroiclabs/nakama-common/runtime"
)

// storage layout
// Collection "{GameTag}"
//		Key "{ServerId}"
//				Data: { MatchId, MaxNumPlayers, NumPlayers }

type MatchStorageData struct {
	GameTag       string `json:"GameTag"`
	ServerId      string `json:"ServerId"`
	MatchId       string `json:"MatchId"`
	MaxNumPlayers int    `json:"MaxNumPlayers"`
	NumPlayers    int    `json:"NumPlayers"`
	NumClients    int    `json:"NumClients"`
}

func writeMatchStorage(data MatchStorageData, gameTag string, serverID string, ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule) error {
	if data, err := json.Marshal(data); err != nil {
		logger.Error("[writeMatchStorage] invalid json format")
		return err
	} else {
		// get encoded json data
		data := string(data)
		// prepare params
		dataKey := "MatchInfo"
		objectIDs := []*runtime.StorageWrite{
			{
				Collection:      gameTag,
				Key:             dataKey,
				UserID:          serverID,
				Value:           data,
				PermissionRead:  2, // Public Read
				PermissionWrite: 1, // Owner Write
			},
		}
		if _, err := nk.StorageWrite(ctx, objectIDs); err != nil {
			logger.Error("[writeMatchStorage] failed to write to storage for (%s, %s)", gameTag, serverID)
			return err
		} else {
			return nil
		}
	}
}

type MatchStorageInitParams struct {
	GameTag       string
	ServerId      string
	MatchId       string
	MaxNumPlayers int
}

// initialize a storage for a match
func initMatchStorage(params MatchStorageInitParams, ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule) error {
	data := MatchStorageData{
		GameTag:       params.GameTag,
		ServerId:      params.ServerId,
		MatchId:       params.MatchId,
		MaxNumPlayers: params.MaxNumPlayers,
		NumPlayers:    0,
		NumClients:    0,
	}
	return writeMatchStorage(data, params.GameTag, params.ServerId, ctx, logger, nk)
}

type MatchStorageCleanParams struct {
	GameTag  string
	ServerId string
}

func cleanMatchStorage(params MatchStorageCleanParams, ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule) error {
	dataKey := "MatchInfo"
	objectIDs := []*runtime.StorageDelete{
		{
			Collection: params.GameTag,
			Key:        dataKey,
			UserID:     params.ServerId,
		},
	}
	if err := nk.StorageDelete(ctx, objectIDs); err != nil {
		logger.Error("[cleanMatchStorage] failed to delete storage for (%s, %s)", params.GameTag, params.ServerId)
		return err
	} else {
		return nil
	}
}

type MatchStorageUpdateParams struct {
	GameTag  string
	ServerId string
	// updated data
	NumPlayers int
	NumClients int
}

func updateMatchStorage(params MatchStorageUpdateParams, ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule) error {
	// ensure match exists
	queryParams := MatchStorageQueryRequestParams{
		GameTag:       params.GameTag,
		ServerId:      params.ServerId,
		MaxNumPlayers: -1,
		MaxNumRecords: 1,
	}
	if res, err := queryMatchStorage(queryParams, ctx, logger, nk); err != nil || len(res) == 0 {
		logger.Error("[updateMatchStorage] storage does not exists for (%s, %s)", params.GameTag, params.ServerId)
		return errors.New("invalid request")
	} else {
		res := res[0]
		data := MatchStorageData{}
		if err := json.Unmarshal([]byte(res.Payload), &data); err != nil {
			logger.Error("[updateMatchStorage] invalid json payload = %s", res.Payload)
			return err
		} else {
			data.NumPlayers = params.NumPlayers
			data.NumClients = params.NumClients
			return writeMatchStorage(data, params.GameTag, params.ServerId, ctx, logger, nk)
		}
	}
}

type MatchStorageQueryRequestParams struct {
	GameTag       string
	ServerId      string // empty string to find all, otherwise only query the server ID and ignore MaxNumPlayers & MaxNumRecords
	MaxNumPlayers int    // set negative to find all, otherwise only query servers with NumPlayers < MaxNumPlayers
	MaxNumRecords int    // negative to query all
}

type MatchStorageQueryReponseParams struct {
	Payload string
}

func queryMatchStorage(params MatchStorageQueryRequestParams, ctx context.Context, logger runtime.Logger, nk runtime.NakamaModule) ([]MatchStorageQueryReponseParams, error) {
	response := []MatchStorageQueryReponseParams{}

	if len(params.ServerId) != 0 {
		// query only one server, ignore MaxNumPlayers
		dataKey := "MatchInfo"
		objectIDs := []*runtime.StorageRead{
			{
				Collection: params.GameTag,
				Key:        dataKey,
				UserID:     params.ServerId,
			},
		}

		if records, err := nk.StorageRead(ctx, objectIDs); err != nil || len(records) == 0 {
			logger.Error("[queryMatchStorage] failed to read storage for (%s, %s)", params.GameTag, params.ServerId)
			return response, err
		} else {
			for _, record := range records {
				if checkMatchExists(record, ctx, nk) {
					response = append(response, MatchStorageQueryReponseParams{
						Payload: record.Value,
					})
					break
				}
			}
		}
	} else {
		// iterate all servers, find matches
		nextCursor := ""
		filterRecord := params.MaxNumPlayers >= 0
		for {
			if params.MaxNumRecords >= 0 && len(response) >= params.MaxNumRecords {
				break
			}

			if records, nextCursor, err := nk.StorageList(ctx, "", params.GameTag, 100, nextCursor); err != nil {
				logger.Error("[queryMatchStorage] failed to list storage for (%s)", params.GameTag)
				return response, err
			} else {
				for _, record := range records {
					if !checkMatchExists(record, ctx, nk) {
						continue
					}

					if params.MaxNumRecords >= 0 && len(response) >= params.MaxNumRecords {
						break
					}

					if filterRecord {
						data := MatchStorageData{}
						// skip if current number of players is full
						if err := json.Unmarshal([]byte(record.Value), &data); err != nil || data.NumPlayers >= params.MaxNumPlayers {
							continue
						}
					}

					// otherwise add record
					response = append(response, MatchStorageQueryReponseParams{
						Payload: record.Value,
					})
				}
				if len(nextCursor) == 0 {
					break
				}
			}
		}
	}

	return response, nil
}

func checkMatchExists(record *api.StorageObject, ctx context.Context, nk runtime.NakamaModule) bool {
	data := MatchStorageData{}
	if err := json.Unmarshal([]byte(record.Value), &data); err != nil {
		removeUser(data.ServerId, ctx, nk)
		return false
	}

	if match, err := nk.MatchGet(ctx, data.MatchId); err != nil || match == nil {
		removeUser(data.ServerId, ctx, nk)
		return false
	}

	return true
}

func removeUser(userId string, ctx context.Context, nk runtime.NakamaModule) {
	nk.AccountDeleteId(ctx, userId, false)
}
