package service

import (
	"context"
	"net"
	"net/http"
	"time"

	"github.com/livekit/client-unity-demo/Protocol~/unity_proto"
	"github.com/livekit/client-unity-demo/UnityAPI~/pkg/config"
	"github.com/livekit/client-unity-demo/UnityAPI~/pkg/logger"
	lksdk "github.com/livekit/server-sdk-go"
	"github.com/rs/cors"
	"github.com/urfave/negroni"
)

type UnityAPI struct {
	config     *config.Config
	httpServer *http.Server
	doneChan   chan struct{}
	closedChan chan struct{}
}

func NewUnityAPI(conf *config.Config) *UnityAPI {
	server := &UnityAPI{
		config:     conf,
		doneChan:   make(chan struct{}),
		closedChan: make(chan struct{}),
	}

	return server
}

func (s *UnityAPI) Start() error {
	lkClient := lksdk.NewRoomServiceClient(s.config.LiveKit.Host, s.config.LiveKit.ApiKey, s.config.LiveKit.SecretKey)

	roomService := unity_proto.NewUnityServiceServer(NewUnityService(s.config, lkClient))

	mux := http.NewServeMux()
	mux.Handle(roomService.PathPrefix(), roomService)

	n := negroni.New(negroni.NewRecovery(), cors.New(cors.Options{
		AllowOriginFunc: func(origin string) bool {
			return true
		},
		AllowedHeaders: []string{"*"},
	}))
	n.UseHandler(mux)

	s.httpServer = &http.Server{Addr: ":8080", Handler: n}
	httpListener, err := net.Listen("tcp", s.httpServer.Addr)
	if err != nil {
		return err
	}

	go func() {
		logger.Infow("starting unity api server")
		if err := s.httpServer.Serve(httpListener); err != http.ErrServerClosed {
			logger.Errorw("could not start server", err)
			s.Stop()
		}
	}()

	<-s.doneChan

	// Shutdown the server
	ctx, cancel := context.WithTimeout(context.Background(), time.Second*5)
	defer cancel()
	_ = s.httpServer.Shutdown(ctx)

	close(s.closedChan)
	return nil
}

func (s *UnityAPI) Stop() {
	close(s.doneChan)
	<-s.closedChan
}
