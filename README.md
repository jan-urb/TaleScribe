# TaleScribe

TaleScribe is a desktop app that turns speech into text on your own computer. Record a conversation or
import an audio file and get a transcript split by speaker, or dictate a quick memo and copy the text.
Transcription runs locally with [transcribe.cpp](https://github.com/handy-computer/transcribe.cpp)
(Whisper for speech, Sortformer for telling speakers apart), so your audio never leaves the machine.

## Features

- **Recordings**: record from the microphone or import an `.mp3`, `.m4a` or `.wav` file. TaleScribe
  transcribes it, splits the text into turns per speaker and saves it.
- **Transcript view** with a built-in player: play, pause, seek and change the volume. Save any
  transcript as a text file.
- **Quick memo**: hold a button, speak, let go, and the text appears. Each press is added to the text.
  Nothing is recorded to disk or saved.
- **Model manager**: download Whisper transcription models (tiny to large-v3-turbo, in several
  quantizations) and a speaker model from inside the app, and choose which one to use.
- **Language**: pick the spoken language, or leave it on **Auto** to detect it.
- **Input device**: choose which microphone to record from.
- **Private**: no account, no cloud. Recordings, transcripts and models stay in a local folder.

## Supported platforms

| OS | CPU | Native build used |
|---|---|---|
| Windows 10 / 11 | x64 | `windows-x86_64-cpu-vulkan` |
| macOS | Apple Silicon (M1 and later) | `macos-arm64-metal` |

Linux and Intel Macs are not supported.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- The **transcribe.cpp native libraries** for your OS (see below). They are not included in this
  repository.
- An internet connection the first time, to download the models (from about 45 MB up to 6 GB each,
  depending on the model you choose).

## Getting started

### 1. Clone the repository

```bash
git clone https://github.com/jan-urb/TaleScribe.git
cd TaleScribe
```

### 2. Download the transcribe.cpp native build for your OS

Go to the [transcribe.cpp releases](https://github.com/handy-computer/transcribe.cpp/releases) and
download the archive for your platform:

| OS | Download | Folder name it must have |
|---|---|---|
| Windows x64 | `transcribe-native-<version>-windows-x86_64-cpu-vulkan.tar.gz` | `transcribe-native-windows-x86_64-cpu-vulkan` |
| macOS Apple Silicon | `transcribe-native-<version>-macos-arm64-metal.tar.gz` | `transcribe-native-macos-arm64-metal` |

> **Version:** TaleScribe is built and tested against **transcribe.cpp 0.2.3**. Newer releases may
> work, but the native interface can change between versions. If a newer one fails to load or crashes,
> use 0.2.3.

Create a `native` folder in the repository root, extract the archive into it, and rename the extracted
folder so the **version number is removed** (it must match the name in the table exactly). The
`.dll` / `.dylib` files must sit directly inside it:

```
TaleScribe/                                  ← repository root
├── native/
│   └── transcribe-native-macos-arm64-metal/
│       ├── libtranscribe.dylib
│       ├── libggml.dylib
│       ├── libggml-base.dylib
│       ├── libggml-cpu.dylib
│       ├── libggml-metal.dylib
│       ├── contract.json
│       └── licenses/
├── TaleScribe/
├── TaleScribe.sln
└── README.md
```

On Windows the folder is `native/transcribe-native-windows-x86_64-cpu-vulkan/`, containing
`transcribe.dll` and the `ggml*.dll` files.

`native/` is in `.gitignore`, so the libraries are never committed.

**macOS only:** files downloaded in a browser are quarantined, and macOS may refuse to load the
libraries. Clear the flag once:

```bash
xattr -dr com.apple.quarantine native
```

### 3. Build and run

```bash
dotnet run --project TaleScribe
```

Or open `TaleScribe.sln` in JetBrains Rider or Visual Studio and run the **TaleScribe** project.

The build copies the native libraries for your OS next to the app automatically.

## First run

1. Open **Settings** (bottom of the sidebar).
2. Under **Transcription model**, click **Download** next to a model, then select it with the radio
   button. A small model such as `whisper-base-Q8_0` (81 MB) is a good start. Larger models are more
   accurate but slower.
3. Under **Preferences → Speaker model**, choose a speaker model and click **Download**. It is needed
   for Recordings, not for Quick memo.
4. Choose your **Input device**.
5. Choose the **Default language**. **Auto** (the default) detects it from the audio, which takes a
   few extra seconds per file. Picking the language is faster and more reliable.
6. Allow microphone access:
   - **macOS** asks the first time you record, for the app you started TaleScribe from (Terminal,
     Rider, …). You can change it later in **System Settings → Privacy & Security → Microphone**;
     **Open system settings** in TaleScribe's Settings takes you straight there.
   - **Windows** doesn't ask. Recording works as long as **Settings → Privacy & security →
     Microphone → Let desktop apps access your microphone** is on (it is by default).

Models are saved in the models folder shown at the top of Settings. Click **Change…** to use another
folder, for example on a larger drive.

## Using TaleScribe

**Recordings**
- **Record** starts recording from the microphone. **Stop & transcribe** ends the recording and
  transcribes it. **Discard** throws it away.
- **Pause** stops recording without ending it, and **Resume** continues. The paused part is left out
  of the audio, and the timer stops with it.
- **Import audio…** transcribes an existing `.mp3`, `.m4a` or `.wav` file.
- While transcribing you can **Cancel**. The audio file stays in the recordings folder, but it isn't
  added to the list.
- Click a recording in the list to open its transcript and play the audio.
- **Save as text…** on the transcript page saves it as a `.txt` file named after the recording: the
  title and date, then each turn with its speaker and start time.

**Quick memo**
- Click **Start**, then press and hold **Hold to talk** while you speak. Release it to transcribe.
- Each press is added to the text. **Copy text** copies it all, and **Clear** starts over.

## Where your data is stored

| OS | Folder |
|---|---|
| macOS | `~/Library/Application Support/TaleScribe` |
| Windows | `%AppData%\TaleScribe` |

It contains:

- `taleScribe.db`: the SQLite database with your recordings and transcripts
- `recordings/`: the recorded `.wav` files
- `models/`: downloaded models (unless you chose another folder in Settings)
- `settings.json`: your settings

## Built with

- [Avalonia UI](https://avaloniaui.net/) 12
- [NAudio](https://github.com/naudio/NAudio)
- [transcribe.cpp](https://github.com/handy-computer/transcribe.cpp)
- Models from [Hugging Face: handy-computer](https://huggingface.co/handy-computer)

## License

TaleScribe is released under the [MIT License](LICENSE).

The transcribe.cpp native libraries are not part of this repository and come with their own MIT
licenses, included in the `licenses/` folder of each release archive.
