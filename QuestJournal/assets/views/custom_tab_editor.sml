<frame layout="540px content"
       background={@Mods/StardewUI/Sprites/MenuBackground}
       border={@Mods/StardewUI/Sprites/MenuBorder}
       border-thickness="36, 36, 40, 36"
       padding="32, 24"
       horizontal-content-alignment="middle">
    <lane orientation="vertical" layout="stretch content" horizontal-content-alignment="middle">
        <label layout="stretch content" bold="true" horizontal-alignment="middle" text={:HeaderText} />

        <label layout="stretch content" margin="0, 16, 0, 4" text={#tabeditor.name} />
        <textinput layout="stretch 64px"
                   max-length="30"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>Name} />

        <lane orientation="horizontal" layout="stretch content" margin="0, 12, 0, 4" vertical-content-alignment="middle">
            <label text={#tabeditor.title} />
            <frame layout="36px 36px" margin="8, 0, 0, 0"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   tooltip={:TitleHelp}>
                <label bold="true" text="?" />
            </frame>
        </lane>
        <textinput layout="stretch 64px"
                   max-length="200"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>TitleFilter} />

        <lane orientation="horizontal" layout="stretch content" margin="0, 12, 0, 4" vertical-content-alignment="middle">
            <label text={#tabeditor.source} />
            <frame layout="36px 36px" margin="8, 0, 0, 0"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   tooltip={:SourceHelp}>
                <label bold="true" text="?" />
            </frame>
        </lane>
        <textinput layout="stretch 64px"
                   max-length="200"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>SourceFilter} />

        <lane orientation="horizontal" layout="stretch content" margin="0, 12, 0, 4" vertical-content-alignment="middle">
            <label text={#tabeditor.category} />
            <frame layout="36px 36px" margin="8, 0, 0, 0"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   tooltip={:CategoryHelp}>
                <label bold="true" text="?" />
            </frame>
        </lane>
        <textinput layout="stretch 64px"
                   max-length="200"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>CategoryFilter} />

        <lane orientation="horizontal" layout="stretch content" margin="0, 12, 0, 4" vertical-content-alignment="middle">
            <label text={#tabeditor.kind} />
            <frame layout="36px 36px" margin="8, 0, 0, 0"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   tooltip={:KindHelp}>
                <label bold="true" text="?" />
            </frame>
        </lane>
        <textinput layout="stretch 64px"
                   max-length="200"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>KindFilter} />

        <lane orientation="horizontal" layout="stretch content" margin="0, 12, 0, 4" vertical-content-alignment="middle">
            <label text={#tabeditor.deadline} />
            <frame layout="36px 36px" margin="8, 0, 0, 0"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   tooltip={:DeadlineHelp}>
                <label bold="true" text="?" />
            </frame>
        </lane>
        <textinput layout="stretch 64px"
                   max-length="10"
                   background={@Mods/StardewUI/Sprites/TextBox}
                   text={<>DeadlineFilter} />

        <lane orientation="horizontal" margin="0, 24, 0, 0" horizontal-content-alignment="middle">
            <frame margin="0, 0, 12, 0"
                   padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Save()|>
                <label text={#tabeditor.save} />
            </frame>
            <frame *if={:ShowDelete}
                   margin="0, 0, 12, 0"
                   padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Delete()|>
                <label text={#tabeditor.delete} />
            </frame>
            <frame padding="20, 12"
                   background={@Mods/StardewUI/Sprites/ButtonLight}
                   horizontal-content-alignment="middle"
                   vertical-content-alignment="middle"
                   focusable="true"
                   click=|Cancel()|>
                <label text={#tabeditor.cancel} />
            </frame>
        </lane>
    </lane>
</frame>
