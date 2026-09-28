# TaleScribe

TaleScribe is a desktop app that turns speech into text on your own computer. Record a conversation or
import an audio file and get a transcript split by speaker, or dictate a quick memo and copy the text.
Transcription runs locally with [transcribe.cpp](https://github.com/handy-computer/transcribe.cpp)
(Whisper for speech, Sortformer for telling speakers apart), so your audio never leaves the machine.

## Features

- **Recordings**: record from the microphone or import an `.mp3`, `.m4a` or `.wav` file. TaleScribe
  transcribes it, splits the text into turns per speaker and saves it. Delete recordings you no
  longer need.
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
- An internet connection the first time: the first build downloads the transcribe.cpp native
  libraries (about 1.5 MB on macOS, 20 MB on Windows), and the app downloads the models (from about
  45 MB up to 6 GB each, depending on the model you choose).

## Getting started

### 1. Clone the repository

```bash
git clone https://github.com/jan-urb/TaleScribe.git
cd TaleScribe
```

### 2. Build and run

```bash
dotnet run --project TaleScribe
```

Or open `TaleScribe.sln` in JetBrains Rider or Visual Studio and run the **TaleScribe** project.

The first build downloads **transcribe.cpp 0.2.3** for your OS from the
[transcribe.cpp releases](https://github.com/handy-computer/transcribe.cpp/releases), checks its
SHA-256 hash and extracts it into `native/` in the repository root. Later builds reuse it. The build
then copies the native libraries next to the app. `native/` is in `.gitignore`, so the libraries are
never committed.

To move to another transcribe.cpp version, change `TranscribeVersion` and the two `NativeSha256`
values in `TaleScribe/TaleScribe.csproj`. The next build downloads the new version.

### Installing the native libraries by hand

Only needed if the build can't download them (for example, offline). Download the archive for your
platform from the [transcribe.cpp 0.2.3 release](https://github.com/handy-computer/transcribe.cpp/releases/tag/v0.2.3):

| OS | Download | Folder it extracts to |
|---|---|---|
| Windows x64 | `transcribe-native-0.2.3-windows-x86_64-cpu-vulkan.tar.gz` | `transcribe-native-windows-x86_64-cpu-vulkan` |
| macOS Apple Silicon | `transcribe-native-0.2.3-macos-arm64-metal.tar.gz` | `transcribe-native-macos-arm64-metal` |

Create a `native` folder in the repository root and extract the archive into it. Then create an empty
file named `.version-0.2.3` in the extracted folder, otherwise the build tries to download it again:

```
TaleScribe/                                  ← repository root
├── native/
│   └── transcribe-native-macos-arm64-metal/
│       ├── .version-0.2.3
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

**macOS only:** files downloaded in a browser are quarantined, and macOS may refuse to load the
libraries. Clear the flag once:

```bash
xattr -dr com.apple.quarantine native
```

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
  of the audio. The dot on the recording screen is red and pulsing while recording, grey while paused.
- **Import audio…** transcribes an existing `.mp3`, `.m4a` or `.wav` file.
- While transcribing you can **Cancel**. Nothing is added to the list; a microphone recording's audio
  file stays in the recordings folder.
- Click a recording in the list to open its transcript and play the audio.
- **Delete** on a row removes the recording and its transcript right away (no confirmation). Audio
  recorded in TaleScribe is deleted from the recordings folder too; an imported file is left where it
  is.
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
