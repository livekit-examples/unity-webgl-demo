PROTO_IN=.
CSHARP_OUT=csharp
GO_OUT=unity_proto
protoc \
    -I=$PROTO_IN \
    --go_out=$GO_OUT \
    --csharp_out=$CSHARP_OUT \
    --twirp_unity_out=$CSHARP_OUT \
    --twirp_out=$GO_OUT \
    --twirp_opt=paths=source_relative \
    --go_opt=paths=source_relative \
    $PROTO_IN/UnityAPI.proto