
# ScaleNet - Scalable C# Networking for MMO Games

ScaleNet is a networking library for C# that is specifically designed around high player counts and MMO game networking requirements.

It is transport-layer agnostic, meaning that you can implement your own transport layer if you want to.
A default TCP transport layer is provided, which is sufficient for most use cases.

ScaleNet targets .NET Standard 2.1 with special optimizations for .NET 8, making it compatible with most recent .NET runtimes, including Unity 2018.3 and later.

> [!NOTE]  
> If you are planning on using ScaleNet with Unity, please read the [Unity Compatibility](#unity-compatibility) section.

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

## Unity Compatibility

> [!WARNING]
> Please note that the library is not yet optimized for Unity and **may** require some modifications to work correctly.

ScaleNet is compatible with Unity 2018.3 and later, but there are some caveats:

ScaleNet includes platform-specific `#ifdef`s and workarounds for Unity, thus it is not available precompiled.
You need to clone the repository and manually copy the source files into your project.

### MessagePack

ScaleNet uses MessagePack for serialization.
MessagePack serializes custom objects by generating IL on the fly at runtime to create custom highly tuned formatters for each message type.

**When using Unity, this dynamic code generation only works when targeting .NET Framework 4.x + mono runtime. For all other Unity targets, manual AOT code generation is required.**

MessagePack provides a tool called `mpc` (MessagePackCompiler) that can generate AOT code for Unity. You can find more information about this in the [MessagePack for C# documentation](https://github.com/MessagePack-CSharp/MessagePack-CSharp?tab=readme-ov-file#aot-code-generation-support-for-unityxamarin).

Here's the quick guide to generate AOT code for Unity:

1. Acquire `mpc` as a dotnet tool.
    Install as global tool:
    ```bash
    dotnet tool install --global MessagePack.Generator
    ```
    or install as local tool. This allows you to include the tools and versions that you use in your source control system. Run these commands in the root of your repo:
    ```bash
    dotnet new tool-manifest
    dotnet tool install MessagePack.Generator
    ```
    If installed as a local tool, on another machine you can "restore" your tool using the `dotnet tool restore` command.
   
2. Generate AOT code for your project:
    ```bash
    dotnet mpc -i "..\src\Sandbox.Shared.csproj" -o "MessagePackGenerated.cs"
    ```
   
3. Add the generated file to your Unity project.

4. In Unity, in your custom script **before using MessagePack**, register the generated resolver:
    ```csharp
    MessagePack.Resolvers.StaticCompositeResolver.Instance.Register(
        MessagePack.Resolvers.GeneratedResolver.Instance, // Custom AOT-generated resolver
        MessagePack.Resolvers.StandardResolver.Instance); // Fallback to standard
    MessagePack.MessagePackSerializerOptions options =
        MessagePack.MessagePackSerializerOptions.Standard
        .WithResolver(MessagePack.Resolvers.StaticCompositeResolver.Instance);
    MessagePack.MessagePackSerializer.DefaultOptions = options;
    ```

---

## Getting Started

### Installation (C# Project)

Currently, the preferred way to use ScaleNet is to clone the repository and manually copy the source files into your project.

```bash
git clone https://github.com/japsuu/ScaleNet
```

The server and client code are separated into different projects in the `/src` directory, so you need to copy the correct files into your project:
- For server, you should copy `ScaleNet.Server` and `ScaleNet` folders into your project.
- For client, you should copy `ScaleNet.Client` and `ScaleNet` folders into your project.

If you do not care about separating the server and client code, you can copy the entire `src` folder into your project.

### Installation (Unity)

> [!WARNING]
> Read the [Unity Compatibility](#unity-compatibility) section before proceeding.

Now, using this library in Unity is complicated. For me, it's worth it.
Here's how I personally use this library in Unity:

#### File structure

I create my basic file structure as follows:
root
- .git
- submodules/
   - ScaleNet (cloned as a git submodule)
- src/
   - Client/ (Unity Project)
      - Assets/
      - ...
   - Server/
      - Server.sln
      - Server/ (C# Project)
      - Common/ (C# Project)

#### Synchronization

> [!NOTE]
> Skip this, if you plan on modifying the ScaleNet source, or otherwise never updating the submodule.

In Unity, I use the ["Link & Sync"](https://assetstore.unity.com/packages/tools/version-control/link-sync-229569) package to sync the ScaleNet submodule to Unity:
- Unity > Import Link & Sync
- "Assets" > "Create" > "Link And Sync" > Move the created sync object to `Assets/Plugins/ScaleNet/Client`
- In the inspector, add `../../submodules/ScaleNet/src/ScaleNet.Client` to "External Directories." You might also want to exclude, for example, the `.csproj` file.
- Click "Prepare Pull" > Execute
- Now do the same for `../../submodules/ScaleNet/src/ScaleNet.Common` into `Assets/Plugins/ScaleNet/Common`.

Now the ScaleNet source is mirrored into your Unity project. Feel free to pull changes to the submodule when needed.

#### Dependencies

Unity doesn't support NuGet packages natively. You can either use [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) or add the required packages as either DLLs or `.unitypackage`s.

Get the following dependencies in one of the aforementioned ways:
- [MessagePackCSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp/releases) (`.unitypackage` recommended)

If you installed MessagePack using the `.unitypackage`, you should also enable MessagePack CodeGen: Unity > Window > MessagePack > CodeGenerator > Install

> [!NOTE]
> Even if using the code generator, you must remember to register the generated AOT code as described in the [Unity Compatibility](#unity-compatibility) section.

### Usage

Please check out the provided example projects to see how to use ScaleNet:

- [TCP Chat](examples/Chat) - A simple chat server and client using the default TCP transport layer

---

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.