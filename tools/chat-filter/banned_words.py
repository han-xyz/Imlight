"""Turns the game client's banned words off or on.

The client masks banned words itself, from two lists in Data/GameData/Root.wad: ChatFilter/BlackListBase.txt (word
pairs) and ChatFilter/CarlinListBase.txt (words and fragments). The server never sees them, so turning them off is a
change to each player's client. Off replaces both with a one-line list nobody types; on puts the client's own back.

Every other entry keeps its exact bytes, so an installed admin panel stays. The untouched archive is kept at
Data/Root.wad.adminpanel-original, the copy the admin panel keeps and the client-folder file share serves to players
who patch. On reads the lists back from it.

Usage (close the game client first):
  python -I banned_words.py --client <Client dir> [--off | --on]
With neither, it prints whether the banned words are on.
"""
import argparse
import importlib.util
import os
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
LISTS = ['ChatFilter/BlackListBase.txt', 'ChatFilter/CarlinListBase.txt']
# UTF-16 with a byte order mark, like the client's own lists. A missing list stops the client's chat filter from starting.
PLACEHOLDER = '﻿zqxj zqxj'.encode('utf-16-le')


def load_wad_tools():
    spec = importlib.util.spec_from_file_location('admin_panel_build', os.path.join(HERE, '..', 'admin-panel', 'build.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


wad = load_wad_tools()


def paths(client):
    return (os.path.join(client, 'Data', 'GameData', 'Root.wad'),
            os.path.join(client, 'Data', 'Root.wad.adminpanel-original'))


def lists_turned_off(root):
    return [name for name in LISTS if wad.read_wad_entry(root, name) == PLACEHOLDER]


def write_lists(root, files):
    patched = root + '.chatfilter-new'
    shift = wad.add_to_wad(root, patched, files)
    wad.verify_patch(root, patched, files, shift)
    os.replace(patched, root)


def turn_off(client):
    root, original = paths(client)
    if len(lists_turned_off(root)) == len(LISTS):
        print('the banned words are already off')
        return

    if not os.path.exists(original):
        shutil.copyfile(root, original)
        print('saved the original Root.wad as ' + original)

    write_lists(root, {name: PLACEHOLDER for name in LISTS})
    print('turned the banned words off in ' + root)


def turn_on(client):
    root, original = paths(client)
    if not lists_turned_off(root):
        print('the banned words are already on')
        return

    if not os.path.exists(original):
        raise SystemExit(f'{original} is missing, so the lists cannot be read back. Repair the client with its launcher.')

    write_lists(root, {name: wad.read_wad_entry(original, name) for name in LISTS})
    print('turned the banned words on in ' + root)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--client', required=True, help='The game client directory (contains Bin and Data)')
    action = parser.add_mutually_exclusive_group()
    action.add_argument('--off', action='store_true')
    action.add_argument('--on', action='store_true')
    arguments = parser.parse_args()
    if arguments.off:
        turn_off(arguments.client)
    elif arguments.on:
        turn_on(arguments.client)
    else:
        print('the banned words are ' + ('off' if lists_turned_off(paths(arguments.client)[0]) else 'on'))
