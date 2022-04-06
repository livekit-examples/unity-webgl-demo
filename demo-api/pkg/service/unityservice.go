package service

import (
	"context"
	"errors"

	"github.com/livekit/client-unity-demo/Protocol~/unity_proto"
	"github.com/livekit/client-unity-demo/UnityAPI~/pkg/config"
	"github.com/livekit/protocol/auth"
	lksdk "github.com/livekit/server-sdk-go"
	"google.golang.org/protobuf/encoding/protojson"
)

var (
	ErrParticipantNameEmpty = errors.New("participant name is empty")
	ErrRoomNameEmpty        = errors.New("room name is empty")
)

type UnityService struct {
	config   *config.Config
	lkClient *lksdk.RoomServiceClient
}

func NewUnityService(config *config.Config, lkClient *lksdk.RoomServiceClient) *UnityService {
	return &UnityService{config: config, lkClient: lkClient}
}

func (s *UnityService) RequestJoinToken(ctx context.Context, req *unity_proto.JoinTokenRequest) (*unity_proto.JoinTokenResponse, error) {
	token := s.lkClient.CreateToken()

	if len(req.ParticipantName) == 0 {
		return nil, ErrParticipantNameEmpty
	}

	if len(req.RoomName) == 0 {
		return nil, ErrRoomNameEmpty
	}

	metadata := &unity_proto.ParticipantMetadata{
		IsHost: req.Host,
	}

	jsonBytes, err := protojson.Marshal(metadata)
	if err != nil {
		return nil, err
	}

	// TODO Check if the RoomName already exists
	grant := &auth.VideoGrant{
		Room:     req.RoomName,
		RoomJoin: true,
	}

	token.SetIdentity(req.ParticipantName).
		AddGrant(grant).
		SetMetadata(string(jsonBytes))

	jwt, err := token.ToJWT()
	if err != nil {
		return nil, err
	}

	return &unity_proto.JoinTokenResponse{
		JoinToken: jwt,
	}, nil
}
