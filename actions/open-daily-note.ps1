# Opens today's daily note in the Journal vault.
#
# This uses Obsidian's registered URI scheme instead of sending the Ctrl+Shift+D hotkey.
# That hotkey is internal to Obsidian, so it only fires when the Obsidian window already has
# focus. The URI is handled by the Obsidian process directly: it works from any app, restores
# the window, and launches Obsidian if it is not running.
#
# Requires the "daily-notes" core plugin, which the vault has enabled.

$vault = 'Journal'

Start-Process ('obsidian://daily?vault=' + [Uri]::EscapeDataString($vault))
