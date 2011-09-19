using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;

namespace PicoFx.ESB {
	public class HttpServer : IObservable<RequestContext>, IDisposable {
		private readonly HttpListener _listener;
		private readonly IObservable<RequestContext> _stream;

		public HttpServer( string url ) {
			_listener = new HttpListener();
			_listener.Prefixes.Add( url );
			_listener.Start();
			_stream = CreateObservableHttpContext();
		}

		#region IDisposable Members

		public void Dispose() {
			_listener.Stop();
		}

		#endregion

		#region IObservable<RequestContext> Members

		public IDisposable Subscribe( IObserver<RequestContext> observer ) {
			return _stream.Subscribe( observer );
		}

		#endregion

		private IObservable<RequestContext> CreateObservableHttpContext() {
			return Observable.Create<RequestContext>(
				obs => Observable.FromAsyncPattern<HttpListenerContext>( _listener.BeginGetContext, _listener.EndGetContext )()
							.Select( c => new RequestContext( c.Request, c.Response ) )
							.Subscribe( obs ) )
				.Repeat()
				.Retry()
				.Publish()
				.RefCount();
		}

		/*
		 *	GET http://picobus.net/ordergiven/subscriptions/
		 *	
		 *	GET http://picobus.net/ordergiven/subscriptions/details/19					-> details
		 *	
		 *	PUT http://picobus.net/ordergiven/subscriptions/
		 *		json:
		 *			- address
		 *			- binding
		 *			- contract (.NET type name)
		 *			- optional transformation Id
		 *			-> returns Id
		 *			
		 *	DELETE http://picobus.net/ordergiven/subscriptions/
		 *	
		 *	PUT http://picobus.net/ordergiven/transformation/
		 *			- (from m in bus.ordergiven from i in m.OrderLines select new { m.OrderDate, i.Quantity }).Throttle(TimeSpan.FromSeconds(10))
		 *			-> returns transformation Id
		 *			
		 *	GET http://picobus.net/ordergiven/retry/										-> list retry queue
		 *	
		 *	GET http://picobus.net/ordergiven/errors/									-> list error queue
		 *
		 *	PUT http://picobus.net/ordergiven/											-> publish message
		 *			- message: json
		 *			-> returns message Id
		 *			
		 *	POST http://picobus.net/ordergiven/errors/EtA9kEERUkm8b0XdWJxiug		-> retry immediately
		 *	
		 *	POST http://picobus.net/ordergiven/retry/EtA9kEERUkm8b0XdWJxiug			-> retry immediately
		 */
		private static void AppendTextToLogFile( string text, string path ) {
			using ( FileStream fileStream = File.Open( path, FileMode.Append ) ) {
				using ( var writer = new StreamWriter( fileStream ) ) {
					writer.WriteLine( text );
				}
			}
		}

		public static void Main( string[] args ) {
			string url = args.Where( a => a.StartsWith( "http" ) ).SingleOrDefault() ?? "http://localhost:5555/";

			Console.WriteLine( "--- PicoBus Server --- v0.1" );
			Console.WriteLine( "Listening to " + url );
			using ( var server = new HttpServer( url ) ) {

				Console.WriteLine( "Spinning up Disruptooooors..." );
				using ( var bus = new Bus() ) {

					using ( server.Subscribe(
						context => {
							string[] parts = context.Request.RawUrl.Split( '/' );

							if ( parts.Contains( "publish" ) ) {

								int message;
								if ( int.TryParse( parts.Last(), out message ) )
									bus.Publish( message );

								Console.WriteLine( context.Request.HttpMethod + "\t" + context.Request.RawUrl + "\t200 OK" );

								context.Respond( new JsonResponse( new { MessageSent = message, At = DateTime.Now } ) );
							}
							else {

								Console.WriteLine( context.Request.HttpMethod + "\t" + context.Request.RawUrl + "\t404 Not Found" );
								context.Respond( new StringResponse( "404 Not found" ) { StatusCode = 404 } );
							}

						}, e => Console.WriteLine( e.ToString() ) ) )

					using ( server.Subscribe( 
						context => {
							string text = string.Format("{0}\t{1}\t200 OK", context.Request.HttpMethod, context.Request.RawUrl);
							AppendTextToLogFile(text, @"logs\server.txt" );
						}) ) {

						Console.WriteLine( "Ready to rock and roll." );
						Console.WriteLine( "Press a key to stop the PicoBus server." );
						Console.ReadLine();
					}
				}
			}
		}
	}
}