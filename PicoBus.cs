using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Web.Script.Serialization;
using CSharpVitamins;
using PicoFx.Disruptor;

namespace PicoFx.ESB {

	public class Bus : IDisposable {
		private const int MessagesToPush = 6000;
		private readonly IDisposable _delivery;
		private readonly JavaScriptSerializer _jsonWriter = new JavaScriptSerializer();
		private readonly Random _random = new Random();
		private readonly Subject<int> _input = new Subject<int>();

		private readonly Disruptor<Message<int>> _inputDisruptor = new Disruptor<Message<int>>( 1000 );
		private readonly Disruptor<Message<int>> _retryDisruptor = new Disruptor<Message<int>>( 10000 );
		private readonly Disruptor<Message<int>> _outputDisruptor = new Disruptor<Message<int>>( 4000 );

		private readonly IDisposable _persistence;
		private readonly IDisposable _retryPersistence;
		private readonly IDisposable _retryPolicy;
		private readonly IDisposable _transformation;

		public Bus() {
			
			_persistence = _inputDisruptor.Consuming.SubscribeOn( Scheduler.NewThread ).Subscribe(
				message => AppendMessageToLogFile( message, @"incoming\messages.js" ) );


			_transformation = _inputDisruptor.Cleaning.SubscribeOn( Scheduler.NewThread ).Subscribe(
				message => {
					_outputDisruptor.Put( message.ChangeValue( message.Value % 31 ) );
					_outputDisruptor.Commit();
				} );


			_delivery = _outputDisruptor.Cleaning.SubscribeOn( Scheduler.NewThread ).Subscribe(
				message => {
					if ( _random.Next( 10 ) < 1 ) {
						message.AddAttempt( statusCode: 404, error: "Something went wrong in the receiving end." );

						if ( message.Attempts.Length > 1 ) {
							AppendMessageToLogFile( message, @"ERROR\messages.js" );
						}
						else {
							_retryDisruptor.Put( message );
							_retryDisruptor.Commit();
						}
					}
					else {
						message.AddAttempt( statusCode: 200 );
						AppendMessageToLogFile( message, @"delivered\messages.js" );
						TotalMessagesDelivered++;
					}
				} );


			_retryPersistence = _retryDisruptor.Cleaning.SubscribeOn( Scheduler.NewThread ).Subscribe(
				message => {
					using ( FileStream fileStream = File.OpenWrite( @"retry\" + message.Id + ".msg" ) )
					using ( var writer = new StreamWriter( fileStream ) )
						writer.WriteLine( _jsonWriter.Serialize( message ) );
				} );


			_retryPolicy = Observable.Interval( TimeSpan.FromSeconds( 10 ) ).SubscribeOn( Scheduler.NewThread ).Subscribe(
				count => {
					foreach ( string file in Directory.EnumerateFiles( @"retry\" ) ) {
						var jsonReader = new JavaScriptSerializer();
						var message = jsonReader.Deserialize<Message<int>>( File.ReadAllText( file ) );

						if ( DateTime.Now.Subtract( message.Attempts.OrderByDescending( a => a.Time ).First().Time ).TotalSeconds > 20 ) {
							_outputDisruptor.Put( message );
							_outputDisruptor.Commit();
						}
						File.Delete( file );
					}
				} );


			//_input = Observable.Range( 9489, MessagesToPush ).Subscribe(
			_input.Subscribe(
				i => {
					_inputDisruptor.Put( new Message<int> { Value = i, PicoType = "System.Int32", Received = DateTime.Now, Id = new ShortGuid( Guid.NewGuid() ).ToString() } );
					_inputDisruptor.Commit();
				},
				e => { throw e; },
				Stop );

		}

		private void AppendMessageToLogFile( Message<int> message, string path ) {
			using ( FileStream fileStream = File.Open( path, FileMode.Append ) ) {
				using ( var writer = new StreamWriter( fileStream ) ) {
					writer.WriteLine( _jsonWriter.Serialize( message ) );
				}
			}
		}

		public int TotalMessagesDelivered { get; set; }

		public void Publish( int message ) {
			_input.OnNext( message );
		}

		public void Stop() {
			_persistence.Dispose();
			_transformation.Dispose();
			_delivery.Dispose();
			_retryPersistence.Dispose();
			_retryPolicy.Dispose();
		}

		public void Dispose() {
			_input.Dispose();
			Stop();
		}
	}

	public struct Message<T> {
		public DeliveryAttempt[] Attempts;
		public string Id;
		public string PicoType;
		public DateTime Received;
		public T Value;

		public void AddAttempt( int statusCode, string error ) {
			Attempts = ( Attempts ?? new DeliveryAttempt[ 0 ] )
				.Union( new[] { new DeliveryAttempt { Error = error, StatusCode = statusCode, Time = DateTime.Now } } )
				.ToArray();
		}

		public void AddAttempt( int statusCode ) {
			AddAttempt( statusCode, string.Empty );
		}

		public Message<T> ChangeValue( T value ) {
			var transformed = new Message<T> {
				Attempts = Attempts,
				Id = Id,
				PicoType = PicoType,
				Received = Received,
				Value = value,
			};
			return transformed;
		}
	}

	public struct DeliveryAttempt {
		public string Error;
		public int StatusCode;
		public DateTime Time;
	}
}