# QingtingRadio

A clean, open-source UWP (Universal Windows Platform) radio player application.

## Features
- Browse and play radio channels
- Country (CNR) direct-live stations
- Search and favorites
- Replay/history support
- Download functionality

## Getting Started
1. Open `QingtingRadio.sln` in Visual Studio (2022 or later, with the UWP workload and Windows SDK 10.0.22621).
2. Open the solution properties and set **Build › Platform** to **x86** or **x64**.
3. Build and run.

### Requirements
- Visual Studio 2022 with the **Universal Windows Platform development** workload
- Windows SDK 10.0.22621
- Windows 10 (build 10.0.10586 or later)

## Private API metadata
All radio-service endpoints and the request-signing routine are provided as a
pre-compiled assembly rather than source:

```
libs/QingtingRadio.Core.dll
```

The main project references it from `QingtingRadio/QingtingRadio.csproj`, so the
solution builds out of the box. The corresponding source project is intentionally
not part of this repository.

If you want to add or change an endpoint, drop a new build of
`QingtingRadio.Core.dll` into `libs/` (it exposes the `QingtingRadio.Https`
namespace: `ServiceURLs` and `ApiSigner`).

## Signing / deployment
Signing certificates are **not** included in this repository. To deploy locally,
create a self-signed test certificate in Visual Studio
(*Project Properties › Packaging › Choose Certificate*) or configure
`PackageCertificateKeyFile` in your local `QingtingRadio.csproj.user`
(git-ignored).

## License
This project is licensed under the [MIT License](LICENSE).
