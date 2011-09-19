using System;
using System.Diagnostics;
using System.Web.Script.Serialization;
using CSharpVitamins;
using Xunit;

namespace PicoFx.ESB {
	public class BusTests {
		public BusTests() {
			Trace.Listeners.Add(new DefaultTraceListener());
		}

		[Fact]
		public void JsonWriterCanWriteMessage() {
			// Arrange
			var sut = new JavaScriptSerializer();

			// Act
			string json = sut.Serialize(new Message<int> {Id = new ShortGuid(Guid.NewGuid()).ToString(), PicoType = "System.Int32", Received = DateTime.Now, Value = 3849});

			// Assert
			Assert.True(json.Length > 0);
		}


		[Fact]
		public void BusCanRouteMessages() {
			// Arrange
			var sut = new Bus();

			// Assert
			Assert.True(sut.TotalMessagesDelivered > 0);
		}

	}
}