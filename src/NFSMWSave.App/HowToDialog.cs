namespace NFSMWSave.App;

internal sealed record HowToGuide(string Platform, string Steps);

internal sealed class HowToDialog : Form
{
    private const string MovingProgress = """


        MOVING PROGRESS TO ANOTHER PLATFORM

        1. Open the old save and use File > Export > JSON or XML.
        2. On the new platform, start a career, finish the prologue and save.
        3. Open that save and use File > Import > JSON or XML.
        4. Tick what to bring over and press Import.
        5. Press Ctrl+S.

        Importing cars replaces every car in the open save. Parts that don't exist on the new platform go back to stock.
        """;

    public static readonly IReadOnlyList<HowToGuide> Guides =
    [
        new("PC", """
            EDITING A PC SAVE

            1. Close Need for Speed: Most Wanted.
            2. Use File > Open from game folder and pick your profile.
               You can also use File > Open and go to Documents\NFS Most Wanted\<alias>\<alias>. The save file has no extension.
            3. Make your edits.
            4. Press Ctrl+S. The first save keeps a dated .bak copy beside the file.
            5. Start the game and load your profile.

            GOOD TO KNOW

            - If the bottom bar says the save was made with a different car list, use Tools > Match My Game's Car List, then save.
            - Changing the alias also renames the profile's folder.
            - With NFSMW Unlimiter, saving also updates the car's file in the profile's Presets folder.
            """),
        new("PlayStation 2", """
            EDITING A PS2 SAVE

            1. Copy the save from your memory card to your PC.
               uLaunchELF can copy it to a USB drive as a .psu file. The editor also opens .max and .cbs saves.
            2. Use File > Open and pick the file.
            3. Make your edits.
            4. Press Ctrl+S. The first save keeps a dated .bak copy beside the file.
            5. Copy the edited save back to your memory card with the same tool, replacing the old save.

            GOOD TO KNOW

            - On PCSX2, use a memory card tool such as mymc to export the save and import it back.
            - Some PAL saves use an earlier layout. They open with Add Car and the Rap Sheet turned off.
            """),
        new("Xbox 360", """
            EDITING A SAVE FROM A REAL XBOX 360

            1. Copy the save from your console to a USB drive.
            2. On your PC, use a tool like Horizon to extract the save. It is a CON package.
            3. Use File > Open and pick the file.
            4. Make your edits and press Ctrl+S. The first save keeps a dated .bak copy beside the file.
            5. Rehash and resign the edited package with Horizon. The console won't accept it otherwise.
            6. Put it back on the USB drive and copy it to your console.

            EDITING A XENIA SAVE

            1. Close the game in Xenia.
            2. Use File > Open and pick the save in Xenia's content folder.
            3. Make your edits and press Ctrl+S.
            4. Start the game. Xenia loads the edited save as it is, with no resigning.
            """),
        new("Original Xbox", """
            EDITING AN ORIGINAL XBOX SAVE

            1. Close the game, then find the save on your Xbox or emulator hard drive. It's in E:\UDATA\4541007B\<save id>. In xbased, that's hdd\Partition1\UDATA\4541007b\<save id>.
               If you downloaded a save as a .zip, unzip it first.
            2. Use File > Open and pick the file named after your alias. It has no extension.
            3. Make your edits.
            4. Press Ctrl+S. The first save keeps a dated .bak copy beside the file.
            5. If you copied the file out, put it back in the same folder, replacing the old one.

            GOOD TO KNOW

            - Saves keep the kind of signature they came with, so a save made in xbased stays valid in xbased.
            - The alias can't be changed on these saves, because the save's folder uses it too.
            """),
        new("GameCube", """
            EDITING A SAVE IN DOLPHIN

            1. Stop the game in Dolphin. Dolphin reads the card when the game boots, and a running game can write its old copy over your edit.
            2. Find your .gci save in Dolphin's GC\<region>\Card A folder.
               If you use a single memory card file, export the save as .gci with Tools > Memory Card Manager.
            3. Use File > Open and pick the .gci file.
            4. Make your edits and press Ctrl+S. The first save keeps a dated .bak copy beside the file.
            5. If you exported the save, import it back with the Memory Card Manager.
            6. Start the game.

            EDITING A SAVE FROM A REAL GAMECUBE

            1. Copy the save to an SD card as a .gci file with a homebrew tool such as GCMM.
            2. Edit it on your PC as above.
            3. Copy it back to your memory card with the same tool.
            """),
    ];

    public HowToDialog(HowToGuide guide)
    {
        Text = $"How to Use: {guide.Platform}";
        Font = new Font("Segoe UI", 9f);
        BackColor = Theme.Back;
        Load += (_, _) => Theme.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(Ui.S(600), Ui.S(520));

        var steps = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            TabStop = false,
            Text = (guide.Steps + MovingProgress).ReplaceLineEndings("\r\n"),
        };
        var close = Ui.Action("Close", () => DialogResult = DialogResult.Cancel, 80);
        AcceptButton = close;
        CancelButton = close;
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Back, Padding = new Padding(Ui.S(4)) };
        bottom.Controls.Add(close);
        Controls.Add(steps);
        Controls.Add(bottom);
        Shown += (_, _) =>
        {
            steps.SelectionLength = 0;
            close.Focus();
        };
    }
}
