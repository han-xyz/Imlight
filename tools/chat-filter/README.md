# Chat filter client files

The game client filters chat itself, from the lists in `ChatFilter/` in `Data/GameData/Root.wad`. The server only tells it whether a player has filtered or open chat (`[Chat] FilterEnabled`).

| List | Used for |
| --- | --- |
| `WhiteListBase.txt`, `WhiteListPhrasesBase.txt` | Filtered chat: only these words and phrases get through. |
| `BlackListBase.txt` | Banned word pairs, also when each word is allowed alone. |
| `CarlinListBase.txt` | Banned words and fragments, found anywhere in a word. |
| `ExceptionListBase.txt` | Words that contain a banned fragment but are allowed (assassin, vacuum). |
| `CharacterReplacementMap.txt` | Look-alike symbols read as letters before the check (`¢` as c, `0` as o). |
| `CapitalLettersExceptions.txt` | Emoticons left alone by the capital letters rule (XD, :D). |

## Turning the banned words off

`banned_words.py` swaps the two banned lists, `BlackListBase.txt` and `CarlinListBase.txt`, for a one-line list nobody types. Each player who wants unmasked chat runs it on their own PC, with the game client closed:

```sh
python -I tools/chat-filter/banned_words.py --client "<game client folder>" --off
```

`--on` puts the client's own lists back, and no flag prints whether they are on. Every other entry in `Root.wad` keeps its bytes, so an installed admin panel stays. The untouched archive is kept at `Data/Root.wad.adminpanel-original`, the same copy the admin panel keeps. The client-folder file share serves that copy to players who patch, so the change never reaches them.

Installing or uninstalling the admin panel starts again from that copy and turns the banned words back on, and so does a client update. Run `--off` again after either.
