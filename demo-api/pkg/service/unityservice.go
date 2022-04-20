package service

import (
	"context"
	"errors"

	"github.com/livekit/client-unity-demo/demo-api/pkg/config"
	"github.com/livekit/client-unity-demo/protocol/unity_proto"
	"github.com/livekit/protocol/auth"
	lksdk "github.com/livekit/server-sdk-go"
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

	grant := &auth.VideoGrant{
		Room:     req.RoomName,
		RoomJoin: true,
	}

	token.SetIdentity(req.ParticipantName).
		AddGrant(grant)

	jwt, err := token.ToJWT()
	if err != nil {
		return nil, err
	}

	return &unity_proto.JoinTokenResponse{
		JoinToken: jwt,
	}, nil
}
