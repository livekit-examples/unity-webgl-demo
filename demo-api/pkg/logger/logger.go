package logger

import (
	"github.com/go-logr/logr"
	"github.com/go-logr/zapr"
	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"
)

var (
	logger = logr.Discard()
)

// valid levels: debug, info, warn, error, fatal, panic
func initLogger(config zap.Config, level string) {
	if level != "" {
		lvl := zapcore.Level(0)
		if err := lvl.UnmarshalText([]byte(level)); err == nil {
			config.Level = zap.NewAtomicLevelAt(lvl)
		}
	}

	logger, _ := config.Build()
	SetLogger(zapr.NewLogger(logger))
}

func InitProduction(logLevel string) {
	initLogger(zap.NewProductionConfig(), logLevel)
}

func InitDevelopment(logLevel string) {
	initLogger(zap.NewDevelopmentConfig(), logLevel)
}

// Note: only pass in logr.Logger with default depth
func SetLogger(l logr.Logger) {
	logger = l.WithCallDepth(1).WithName("ifl-cloud")
}

func Debugw(msg string, keysAndValues ...interface{}) {
	logger.V(1).Info(msg, keysAndValues...)
}

func Infow(msg string, keysAndValues ...interface{}) {
	logger.Info(msg, keysAndValues...)
}

func Warnw(msg string, err error, keysAndValues ...interface{}) {
	if err != nil {
		keysAndValues = append([]interface{}{"error", err}, keysAndValues...)
	}
	logger.Info(msg, keysAndValues...)
}

func Errorw(msg string, err error, keysAndValues ...interface{}) {
	logger.Error(err, msg, keysAndValues...)
}
