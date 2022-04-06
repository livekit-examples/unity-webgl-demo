package config

import (
	"fmt"

	"gopkg.in/yaml.v3"
)

type LiveKitConfig struct {
	Host      string `yaml:"host"`
	ApiKey    string `yaml:"api_key"`
	SecretKey string `yaml:"secret_key"`
}

type Config struct {
	Development bool          `yaml:"development"`
	LogLevel    string        `yaml:"log_level"`
	LiveKit     LiveKitConfig `yaml:"livekit"`
}

func NewConfig(confString string) (*Config, error) {
	conf := &Config{}

	if confString != "" {
		if err := yaml.Unmarshal([]byte(confString), conf); err != nil {
			return nil, fmt.Errorf("could not parse config: %v", err)
		}
	}

	return conf, nil
}
