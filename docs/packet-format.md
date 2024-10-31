
# Packet Format

A custom binary format is used to send data between the client and the game server.

## Packet Structure

The packet structure is as follows:

|        Bits |  Field  | Description                        |
|------------:|:-------:|:-----------------------------------|
|     0-7 (8) | version | The version of the packet format.  |
|    8-15 (8) |  type   | The type of the packet.            |
|  16-31 (16) | length  | The length of the payload.         |
|       32... | payload | The data payload of the packet.    |