using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PicoFx.ESB {
	public class Response {
		public Response() {
			WriteStream = s => { };
			StatusCode = 200;
			Headers = new Dictionary<string, string> { { "Content-Type", "text/html" } };
		}

		public int StatusCode { get; set; }
		public IDictionary<string, string> Headers { get; set; }
		public Action<Stream> WriteStream { get; set; }
	}

	public class StringResponse : Response {
		public StringResponse( string message ) {
			var bytes = Encoding.UTF8.GetBytes( message );
			WriteStream = s => s.Write( bytes, 0, bytes.Length );
		}
	}

	public class JsonResponse : Response {
		private static readonly JavaScriptSerializer JavaScriptSerializer = new JavaScriptSerializer();
		public JsonResponse( object obj ) {
			var message = JavaScriptSerializer.Serialize( obj );
			var bytes = Encoding.UTF8.GetBytes( message );
			WriteStream = s => s.Write( bytes, 0, bytes.Length );
		}
	}

	public class EmptyResponse : Response {
		public EmptyResponse( int statusCode = 204 ) {
			StatusCode = statusCode;

		}
	}
}