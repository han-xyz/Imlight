"""Builds the admin panel's client files and optionally installs them into a game client.

The client loads client-process scripts only from its own archives, so installing adds the admin panel's
files to Data/GameData/Root.wad. Existing entries keep their exact bytes; the larger entry table shifts their
offsets, and the new files' data is appended to the end. The untouched original is kept at
Data/Root.wad.adminpanel-original, and reinstalling always starts from it.

The GUI files are derived at build time from the installed client's own settings and login windows, so no
KingsIsle content is stored in this repository.

Usage (close the game client first):
  python -I build.py --client <Client dir> [--install | --uninstall]
"""
import argparse
import os
import shutil
import struct
import xml.etree.ElementTree as ET
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
OUTPUT = os.path.join(HERE, 'out')
TEMPLATE_SCRIPT = os.path.join(HERE, 'client', 'Scripts', 'AdminPanel', 'Client.lua')
SCRIPT_ENTRY = 'Scripts/AdminPanel/Client.lua'
SETTINGS_BUTTON_ENTRY = 'GUI/AdminPanelSettingsButton.gui'
PANEL_ENTRY = 'GUI/AdminPanel.gui'

# Files earlier test builds installed, removed on install and uninstall.
OBSOLETE_FILES = ['AdminPanel.wad', 'Scripts/AdminPanelLoose/Client.lua', 'GUI/AdminPanelTestLoose.gui']
# Folders the loose-file test created; removed only when empty. Children come before their parents.
OBSOLETE_FOLDERS = ['Scripts/AdminPanelLoose', 'Scripts', 'GUI']


def wad_entries(path):
    """Yields (name, offset, size, packed, compressed, crc) for each entry, in table order."""
    with open(path, 'rb') as handle:
        if handle.read(5) != b'KIWAD':
            raise ValueError(f'{path} is not a KIWAD archive')
        version, count = struct.unpack('<II', handle.read(8))
        if version != 1:
            raise ValueError(f'{path} is KIWAD version {version}; only version 1 is supported')
        for _ in range(count):
            offset, size, packed, compressed, crc, length = struct.unpack('<IIIBII', handle.read(21))
            yield handle.read(length).rstrip(b'\0').decode('utf-8'), offset, size, packed, bool(compressed), crc


def read_wad_entry(path, wanted):
    for name, offset, size, packed, compressed, _crc in wad_entries(path):
        if name == wanted:
            with open(path, 'rb') as handle:
                handle.seek(offset)
                data = handle.read(packed if compressed else size)
            return zlib.decompress(data) if compressed else data
    raise KeyError(f'{wanted} not found in {path}')


def kingsisle_crc(data):
    """Reflected CRC-32 (polynomial 0x04C11DB7) with zero initial value and no final XOR, over stored bytes."""
    crc = 0
    for byte in data:
        crc = _CRC_TABLE[(crc ^ byte) & 0xFF] ^ (crc >> 8)
    return crc


def _make_table():
    table = []
    for index in range(256):
        value = index
        for _ in range(8):
            value = (value >> 1) ^ 0xEDB88320 if value & 1 else value >> 1
        table.append(value)
    return table


_CRC_TABLE = _make_table()


def add_to_wad(original, output, files):
    """Writes a copy of a version 1 archive with files added or replaced.

    The archive's data region is copied byte for byte after the larger entry table, so existing entries keep
    their bytes and checksums and only their offsets shift. New data is appended after it.
    """
    replaced = [entry for entry in wad_entries(original) if entry[0] not in files]
    first_data = min(entry[1] for entry in replaced)
    with open(original, 'rb') as handle:
        handle.seek(0, os.SEEK_END)
        data_length = handle.tell() - first_data

    stored = {name: zlib.compress(data, 9) for name, data in files.items()}
    names = [entry[0] for entry in replaced] + list(files)
    header_length = 13 + sum(21 + len(name.encode('utf-8')) + 1 for name in names)
    shift = header_length - first_data

    entries = [(name, offset + shift, size, packed, compressed, crc)
               for name, offset, size, packed, compressed, crc in replaced]
    offset = header_length + data_length
    for name, data in files.items():
        entries.append((name, offset, len(data), len(stored[name]), True, kingsisle_crc(stored[name])))
        offset += len(stored[name])

    # The client expects the table sorted by name in byte order.
    entries.sort(key=lambda entry: entry[0].encode('utf-8'))
    table = []
    for name, entry_offset, size, packed, compressed, crc in entries:
        encoded = name.encode('utf-8') + b'\0'
        table.append(struct.pack('<IIIBII', entry_offset, size, packed, int(compressed), crc, len(encoded)) + encoded)
    header = b'KIWAD' + struct.pack('<II', 1, len(entries)) + b''.join(table)
    if len(header) != header_length:
        raise RuntimeError('The entry table length was miscalculated')

    with open(original, 'rb') as source, open(output, 'wb') as target:
        target.write(header)
        source.seek(first_data)
        shutil.copyfileobj(source, target, 16 * 1024 * 1024)
        for name in files:
            target.write(stored[name])
    return shift


def gui_templates(client, path, names):
    """Returns the named controls of one of the client's GUI files, read from the untouched archive."""
    root_wad, original = paths(client)
    source = read_wad_entry(original if os.path.exists(original) else root_wad, path)
    found = {element.findtext('m_sName'): element for element in ET.fromstring(source).iter('Class')
             if element.findtext('m_sName') in names}
    missing = set(names) - set(found)
    if missing:
        raise KeyError(f'{path} has no {", ".join(sorted(missing))}')
    return found


def control(template, **values):
    """Copies a control without its children and sets its simple fields (m_sName=..., m_Window=...)."""
    copy = ET.fromstring(ET.tostring(template))
    for children in copy.findall('m_Children'):
        copy.remove(children)
    for tag, value in values.items():
        element = copy.find(tag)
        if element is None:
            raise KeyError(f'{template.findtext("m_sName")} has no {tag}')
        element.text = value
    return copy


def rect(left, top, width, height):
    return f'{left},{top},{left + width},{top + height}'


def subtree(template, renames, **values):
    """Copies a control with its children, renaming children by {old name: new name}."""
    copy = ET.fromstring(ET.tostring(template))
    for element in copy.iter('Class'):
        name = element.find('m_sName')
        if element is not copy and name is not None and name.text in renames:
            name.text = renames[name.text]
    for tag, value in values.items():
        copy.find(tag).text = value
    return copy


def attach(window, children):
    """Adds children to a window; like the client's own files, each child has its own m_Children and they come
    before the window's style."""
    style = window.find('m_pWindowStyle')
    position = list(window).index(style) if style is not None else len(window)
    for offset, child in enumerate(children):
        holder = ET.Element('m_Children')
        holder.append(child)
        window.insert(position + offset, holder)
    return window


def gui_file(window, children):
    attach(window, children)
    root = ET.Element('Objects')
    root.append(window)
    ET.indent(root, '    ')
    return ET.tostring(root, encoding='utf-8')


def build_settings_button(client):
    """The settings window's Quit button, relabeled, in the free strip above it."""
    quit_button = gui_templates(client, 'GUI/Settings.gui', ['QuitButton'])['QuitButton']
    left, top, right, bottom = (int(value) for value in quit_button.findtext('m_Window').split(','))
    width, height = right - left, bottom - top

    window = ET.Element('Class', Name='class Window')
    for tag, value in (('m_sName', 'adminpanel_open_window'), ('m_Style', 'HAS_NO_BORDER'), ('m_Flags', 'VISIBLE'),
                       ('m_Window', rect(left, top - height - 6, width, height)), ('m_fTargetAlpha', '1'),
                       ('m_fAlpha', '1'), ('m_Offset', '0,0'), ('m_Scale', '1,1'), ('m_ParentOffset', '0,0,0,0')):
        ET.SubElement(window, tag).text = value
    button = control(quit_button, m_sName='adminpanel_open', m_Window=rect(0, 0, width, height), m_sLabel='Admin',
                     m_sTip='Open the admin panel')
    return gui_file(window, [button])


# The settings book's background draws seven outlined rows; these are their tops within a page.
ROW_TOPS = [10, 58, 106, 152, 198, 244, 291]


def build_panel(client):
    """The admin panel, laid out like a page of the settings window and built from its parts.

    One page per section, each with its own row of buttons; a tab above the book switches between them. Rows
    hold a label and a text field or an arrow selector; the last rows hold the looked-up account's details and
    the result of the last request. Window names must match the ones Client.lua uses.
    """
    parts = gui_templates(client, 'GUI/Settings.gui', [
        'SettingPage', 'Background', 'TabWindow', 'VideoTab', 'PrivacyButton', 'CreditsButton', 'TitleWindow',
        'VideoOptionsText',
        'VideoOptions', 'OptionResolution', 'OptionText', 'OptionControl', 'OptionValue', 'ButtonBackground',
        'QuitButton', 'IdleButton', 'HelpButton', 'OkButton'])
    edits = gui_templates(client, 'GUI/GameLoginWindow.gui', ['loginName', 'loginPassword'])
    value_style = parts['OptionValue'].find('m_pWindowStyle')

    def text_field(name, template):
        # A settings value box with a borderless login text field inset in it, so typed text clears the box's
        # frame. The settings font draws small capitals, which would hide the case of usernames, so fields use
        # the normal font.
        width, height, inset = 244, 30, 10
        box = control(parts['OptionValue'], m_sName=name + '_box', m_Window=rect(0, 0, width, height),
                      m_Flags='VISIBLE', m_sText='')
        edit = control(edits[template], m_sName=name, m_Style='HAS_NO_BORDER', m_Flags='VISIBLE',
                       m_Window=rect(inset, 1, width - 2 * inset, height - 2), m_nMaxLength='40')
        edit.remove(edit.find('m_pWindowStyle'))
        style = ET.fromstring(ET.tostring(value_style))
        for font in style.iter('m_sFilename'):
            if font.text == 'WizardFancy.xml':
                font.text = 'WizardNormal.xml'
        edit.append(style)
        field = control(parts['OptionControl'], m_sName=name + '_field', m_Flags='VISIBLE',
                        m_Window=rect(336, 12, width, height))
        return attach(field, [box, edit])

    def selector(prefix):
        return subtree(parts['OptionControl'], {'OptionButtonLeft': prefix + '_prev', 'OptionValue': prefix + '_value',
                                                'OptionButtonRight': prefix + '_next'}, m_sName=prefix)

    def row(page, index, label, value):
        top = ROW_TOPS[index]
        window = control(parts['OptionResolution'], m_sName=f'{page}_row{index}', m_Window=rect(0, top, 580, 54))
        return attach(window, [control(parts['OptionText'], m_sName=f'{page}_row{index}_label', m_sText=label), value])

    def line(page, name, index):
        # A row-wide line of smaller text, for details and results.
        top = ROW_TOPS[index]
        window = control(parts['OptionResolution'], m_sName=f'{page}_row{index}', m_Window=rect(0, top, 580, 54))
        return attach(window, [control(parts['OptionText'], m_sName=name, m_Window='36,6,560,48', m_sText='')])

    def page(name, visible, rows):
        flags = 'VISIBLE|NOCLIP' if visible else 'NOCLIP'
        return attach(control(parts['VideoOptions'], m_sName=name, m_Flags=flags), rows)

    def buttons(name, visible, specs):
        # A page's row of buttons, in the slots of the settings window's Quit, Idle, Help and OK buttons.
        slots = [parts[template] for template in ('QuitButton', 'IdleButton', 'HelpButton', 'OkButton')]
        left, top, _, bottom = (int(value) for value in slots[0].findtext('m_Window').split(','))
        right = int(slots[-1].findtext('m_Window').split(',')[2])
        group = control(parts['OptionControl'], m_sName=name, m_Flags='VISIBLE' if visible else 'NOCLIP',
                        m_Window=f'{left},{top},{right},{bottom}')
        children = []
        for slot, (button_name, label, tip) in zip(slots, specs):
            x1, y1, x2, y2 = (int(value) for value in slot.findtext('m_Window').split(','))
            children.append(control(slot, m_sName=button_name, m_sLabel=label, m_sTip=tip,
                                    m_Window=f'{x1 - left},{y1 - top},{x2 - left},{y2 - top}'))
        return attach(group, children)

    def tab(index, icon, name, tip):
        # The settings window's tabs: a tab shape with an icon button on it, 67 apart.
        left = 14 + 67 * index
        return [control(parts['VideoTab'], m_sName=name + '_back', m_Window=rect(left, 19, 66, 72)),
                control(parts[icon], m_sName=name, m_Window=rect(left + 8, 27, 50, 50), m_sTip=tip)]

    accounts = page('ap_page_accounts', True, [
        row('ap', 0, 'Username', text_field('ap_edit_username', 'loginName')),
        row('ap', 1, 'Auth Level', selector('ap_level')),
        row('ap', 2, 'Locked', selector('ap_locked')),
        row('ap', 3, 'New Password', text_field('ap_edit_password', 'loginPassword')),
        line('ap', 'ap_summary', 4),
        line('ap', 'ap_details', 5),
        line('ap', 'ap_status', 6),
    ])
    players = page('ap_page_players', False, [
        row('ap_p', 0, 'Username', text_field('ap_p_edit_username', 'loginName')),
        row('ap_p', 1, 'Crowns', text_field('ap_p_edit_crowns', 'loginName')),
        row('ap_p', 2, 'Give Crowns', text_field('ap_p_edit_give', 'loginName')),
        line('ap_p', 'ap_p_summary', 4),
        line('ap_p', 'ap_p_details', 5),
        line('ap_p', 'ap_p_status', 6),
    ])

    tabs = attach(control(parts['TabWindow'], m_sName='ap_tabs'),
                  tab(0, 'PrivacyButton', 'ap_tab_accounts', 'Accounts') +
                  tab(1, 'CreditsButton', 'ap_tab_players', 'Players'))
    title = attach(control(parts['TitleWindow'], m_sName='ap_title_window'), [
        control(parts['VideoOptionsText'], m_sName='ap_title', m_sText='<center>Accounts</center>'),
    ])

    window = control(parts['SettingPage'], m_sName='ap_window', m_Flags='VISIBLE|HCENTER|VCENTER')
    return gui_file(window, [
        control(parts['Background'], m_sName='ap_background'),
        tabs,
        title,
        accounts,
        players,
        control(parts['ButtonBackground'], m_sName='ap_button_background'),
        buttons('ap_buttons_accounts', True, [
            ('ap_btn_lookup', 'Look Up', 'Load the account named in Username'),
            ('ap_btn_create', 'Create', 'Create a new account with this name, password, level and lock'),
            ('ap_btn_save', 'Save', 'Save your changes to the loaded account'),
            ('ap_btn_close', 'Close', 'Close the panel and turn admin mode off'),
        ]),
        buttons('ap_buttons_players', False, [
            ('ap_p_btn_lookup', 'Look Up', 'Load the Crowns of the account named in Username'),
            ('ap_p_btn_give', 'Give', 'Add the Give Crowns amount; a negative amount takes Crowns away'),
            ('ap_p_btn_save', 'Save', 'Set the Crowns to the amount in the Crowns row'),
            ('ap_p_btn_close', 'Close', 'Close the panel and turn admin mode off'),
        ]),
    ])


def build(client):
    shutil.rmtree(OUTPUT, ignore_errors=True)
    os.makedirs(OUTPUT)
    with open(TEMPLATE_SCRIPT, 'rb') as handle:
        script = handle.read()

    files = {
        SCRIPT_ENTRY: script,
        SETTINGS_BUTTON_ENTRY: build_settings_button(client),
        PANEL_ENTRY: build_panel(client),
    }
    for name, data in files.items():
        path = os.path.join(OUTPUT, *name.split('/'))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'wb') as handle:
            handle.write(data)
    print(f'built {len(files)} files in {os.path.abspath(OUTPUT)}')
    return files


def paths(client):
    game_data = os.path.join(client, 'Data', 'GameData')
    return os.path.join(game_data, 'Root.wad'), os.path.join(client, 'Data', 'Root.wad.adminpanel-original')


def remove_obsolete(client):
    game_data = os.path.join(client, 'Data', 'GameData')
    for name in OBSOLETE_FILES:
        path = os.path.join(game_data, *name.split('/'))
        if os.path.exists(path):
            os.remove(path)
            print('removed ' + path)
    for name in OBSOLETE_FOLDERS:
        path = os.path.join(game_data, *name.split('/'))
        if os.path.isdir(path) and not os.listdir(path):
            os.rmdir(path)


def install(client, files):
    root, original = paths(client)
    if not os.path.exists(original):
        shutil.copyfile(root, original)
        print('saved the original Root.wad as ' + original)

    patched = root + '.adminpanel-new'
    shift = add_to_wad(original, patched, files)
    verify_patch(original, patched, files, shift)
    os.replace(patched, root)
    remove_obsolete(client)
    print(f'installed {len(files)} files into {root}')


def verify_patch(original, patched, files, shift):
    """Proves the patched archive serves the new files and matches the original everywhere else."""
    for name, data in files.items():
        if read_wad_entry(patched, name) != data:
            raise RuntimeError(f'{name} did not round-trip through the patched archive')

    before = list(wad_entries(original))
    after = {entry[0]: entry for entry in wad_entries(patched)}
    for name, offset, *rest in before:
        if name not in files and after.get(name) != (name, offset + shift, *rest):
            raise RuntimeError(f'The patched archive changed the entry {name}')

    first_data = min(entry[1] for entry in before)
    with open(original, 'rb') as a, open(patched, 'rb') as b:
        a.seek(first_data)
        b.seek(first_data + shift)
        while True:
            left = a.read(16 * 1024 * 1024)
            if not left:
                break
            if b.read(len(left)) != left:
                raise RuntimeError('The patched archive changed existing data')


def uninstall(client):
    root, original = paths(client)
    if os.path.exists(original):
        os.replace(original, root)
        print('restored the original ' + root)
    remove_obsolete(client)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--client', required=True, help='The game client directory (contains Bin and Data)')
    action = parser.add_mutually_exclusive_group()
    action.add_argument('--install', action='store_true')
    action.add_argument('--uninstall', action='store_true')
    arguments = parser.parse_args()
    if arguments.uninstall:
        uninstall(arguments.client)
    else:
        built = build(arguments.client)
        if arguments.install:
            install(arguments.client, built)
