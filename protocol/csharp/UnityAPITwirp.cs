
using Unity_protocol;
using UnityEngine;

// source: UnityAPI.proto
namespace Twirp {
	
	public class UnityServiceClient : TwirpClient {

		public UnityServiceClient(MonoBehaviour mono, string url, int timeout, string serverPathPrefix="twirp", TwirpHook hook=null) : base(mono, url, timeout, serverPathPrefix, hook) {

		}
		
		public TwirpRequestInstruction<JoinTokenResponse> RequestJoinToken(JoinTokenRequest request){
			return MakeRequest<JoinTokenRequest, JoinTokenResponse>("unity_protocol.UnityService/RequestJoinToken", request);
		}
	}
}
