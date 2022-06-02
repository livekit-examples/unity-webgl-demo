package service

import (
	"context"
	"fmt"
	"github.com/livekit/client-unity-demo/protocol/unity_proto"
	"net"
	"net/http"
	"path/filepath"
	"strings"
	"time"

	"github.com/livekit/client-unity-demo/demo-api/pkg/config"
	"github.com/livekit/client-unity-demo/demo-api/pkg/logger"
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
	mux.Handle("/", http.FileServer(http.Dir(s.config.BuildPath)))

	n := negroni.New()
	n.Use(negroni.NewRecovery())
	n.Use(cors.New(cors.Options{
		AllowOriginFunc: func(origin string) bool {
			return true
		},
		AllowedHeaders: []string{"*"},
	}))
	n.UseFunc(func(rw http.ResponseWriter, r *http.Request, next http.HandlerFunc) {
		// Support Brotli compression & wasm streaming
		filename := filepath.Base(r.URL.Path)
		exts := strings.Split(filename, ".")
		headers := rw.Header()

		if filename == "unity.loader.js" || filename == "index.html" || r.URL.Path == "/" {
			headers.Set("Cache-Control", "no-cache, no-store, must-revalidate")
			headers.Set("Pragma", "no-cache")
			headers.Set("Expires", "0")
		}

		if strings.Contains(r.Header.Get("Accept-Encoding"), "br") && len(exts) >= 1 {

			if exts[len(exts)-1] == "br" {
				headers.Set("Content-Encoding", "br")
			}

			if len(exts) >= 2 {
				switch exts[len(exts)-2] {
				case "wasm":
					headers.Set("Content-Type", "application/wasm")
				case "js":
					headers.Set("Content-Type", "application/javascript")
				}
			}
		}

		next.ServeHTTP(rw, r)
	})
	n.UseHandler(mux)

	s.httpServer = &http.Server{
		Addr:    fmt.Sprintf(":%d", s.config.Port),
		Handler: n,
	}

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
