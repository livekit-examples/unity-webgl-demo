FROM golang:1.17-alpine as builder

WORKDIR /workspace

# Copy the Go Modules manifests
COPY go.mod go.mod
COPY go.sum go.sum
# cache deps before building and copying source so that we don't need to re-download as much
# and so that source changes don't invalidate our downloaded layer
RUN go mod download

# Copy the go source
COPY cmd/ cmd/
COPY pkg/ pkg/

RUN go build -o unity-api ./cmd/server

FROM alpine

COPY --from=builder /workspace/unity-api /unity-api
COPY unity/ unity/

# Run the binary.
ENTRYPOINT ["./unity-api"]