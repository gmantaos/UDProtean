UDProtean
[![ ](https://img.shields.io/nuget/v/UDProtean.svg)](https://www.nuget.org/packages/UDProtean)
[![ ](https://img.shields.io/nuget/dt/UDProtean.svg)](https://www.nuget.org/packages/UDProtean)
[![ ](https://ci.appveyor.com/api/projects/status/vcrn0rrl91yo54ai/branch/master?svg=true)](https://ci.appveyor.com/project/gmantaos/udprotean/branch/master)
[![ ](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
==========


Primarily with multiplayer games in mind, where the communication between the server and the client is made up of very frequent and small time-sensitive messages. Obviously the TCP protocol never satisfied cases like that. UDP on the other hand usually required a lot of wrapping and delivery acknowledgements, to make sure that nothing important is lost, or delivered out of order.

What this library does, is provive under-the-hood sequential communication, so that the API on which messages are received and handled can assume that everything is sent, delivered and handled in the order in which it was sent.
Additionally, it also handles fragmentation and defragmentation of large datagrams so that applications built on top of it can leverage a message-centric method of communication, with no concern of the underlying UDP limitations or the MTUs.

**Disclaimer:** The current state of this library will function as described when there is a constant flow in communication. What's still left to be implemented is a more proactive mechanism that repeats message deliveries by itself and not only on bad acknowledgements.

## Usage

### Getting Started

To get started quickly, you can register an event handler for incoming messages.

#### Server

```csharp
UDPServer server = new UDPServer(5000);

server.OnData += (endPoint, data) =>
{

};

server.Start();
```

#### Client

```csharp
UDPClient client = new UDPClient("127.0.0.1", 5000);

// Performs the handshake to establish the connection
client.connect();

client.Send("hello world");
```

### Server client behaviors

However, the point of this library is to treat each client as maintained end-to-end connection. For this purpose you want to define a class which implements `UDPClientBehavior`.

```csharp
class EchoClient : UDPClientBehavior
{
	protected override void OnOpen()
	{
		Console.WriteLine("Client connected from: " + EndPoint.Address);
	}

	protected override void OnClose()
	{
		Console.WriteLine("Client disconnected");
	}

	protected override void OnData(byte[] data)
	{
		Console.WriteLine("Received {0} bytes of data", data.Length);		

		// Echo it back
		Send(data);
	}

	protected override void OnError(Exception ex)
	{
	}
}
```

And then simply start it like before.

```csharp
UDPServer<EchoClient> server = new UDPServer<EchoClient>(5000);
server.Start();
```
