using System.Linq;
using System.Net;

namespace PicoFx.ESB
{
    public class RequestContext
    {
        private readonly HttpListenerResponse listenerResponse;

        public RequestContext(HttpListenerRequest request, HttpListenerResponse response)
        {
            listenerResponse = response;
            Request = MapRequest(request);
        }

        private static Request MapRequest(HttpListenerRequest request)
        {
            var mapRequest = new Request
                                 {
                                     Headers = request.Headers.ToDictionary(),
                                     HttpMethod = request.HttpMethod,
                                     InputStream = request.InputStream,
                                     RawUrl = request.RawUrl
                                 };
            return mapRequest;
        }

        public Request Request { get; private set; }

        public void Respond(Response response)
        {
            foreach (var header in response.Headers.Where(r => r.Key != "Content-Type"))
            {
                listenerResponse.AddHeader(header.Key, header.Value);
            }

            listenerResponse.ContentType = response.Headers["Content-Type"];
            listenerResponse.StatusCode = response.StatusCode;

            using (var output = listenerResponse.OutputStream)
            {
                response.WriteStream(output);
            }
        }
    }
}