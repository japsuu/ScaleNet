
# ScaleNet - Scalable C# Networking for MMO Games

ScaleNet is a networking library for C# that is specifically designed around high player counts and MMO game networking requirements.

It is transport-layer agnostic, meaning that you can implement your own transport layer if you want to.
A default TCP transport layer is provided, which is sufficient for most use cases.

**This is a personal project that I am working on in my free time.
This project was created to serve a specific need that I had, since I couldn't find any networking libraries that met my requirements.
I am sharing this project in the hope that it will be useful to others.**

---

## Features

### General

- Designed for MMO games, but can be used in any type of game
- Transport-layer agnostic (default TCP transport layer provided)
- Easy to use and extend
- Mostly lock-free and thread-safe
- Strong separation between client and server code to avoid leaking server code to clients
- Uses modern C# features for maximum performance (`Span<T>`, `Memory<T>`, etc.)

### Networking

- Supports 5000+ concurrent connections per server
- Handles packet fragmentation and reassembly (with length prefix framing)
- Built-in packet middleware support (compression, encryption, etc.)

### Messaging System

ScaleNet implements a messaging system that allows you to define your own message types and message handlers. Messages are internally serialized and deserialized using [MessagePack](https://msgpack.org/).

- Define your own message types with a simple C# struct
- Define message handlers for each message type
- Thread-safe message handling
- Send messages from client to server and from server to client
- Send messages to all clients, to a specific client, or to a group of clients

### Client Management

ScaleNet provides basic client management features, such as:

- Client authentication (register, login)
- Automatic client/server version checking on first connection
- Server-side rate limiting
- Server-side cheat detection

---

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later

### Installation

> [!NOTE]  
> This project is still in development and is not yet available on NuGet.

Currently, the preferred way to use ScaleNet is to clone the repository and manually copy the source files into your project.

```bash
git clone https://github.com/japsuu/ScaleNet
```

The server and client code are separated into different projects in the `/src` directory, so you need to copy the correct files into your project:
- For your server project, you should copy the `ScaleNet.Server` and `ScaleNet` folders into your project.
- For your client project, you should copy the `ScaleNet.Client` and `ScaleNet` folders into your project.

### Usage

Please check out the provided example projects to see how to use ScaleNet:

- [TCP Chat](examples/Chat) - A simple chat server and client using the default TCP transport layer

---

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.