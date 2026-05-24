<lane orientation="vertical"
      horizontal-content-alignment="middle"
      vertical-content-alignment="middle">
    <frame layout="1100px 680px"
           background={@Mods/StardewUI/Sprites/MenuBackground}
           border={@Mods/StardewUI/Sprites/MenuBorder}
           border-thickness="36, 36, 40, 36"
           padding="20, 12">
        <lane orientation="vertical" horizontal-content-alignment="middle">

            <lane orientation="horizontal" margin="0, 0, 0, 8">
                <frame *repeat={:Tabs}
                       layout="140px 56px"
                       margin="0, 0, 8, 0"
                       padding="12, 0"
                       horizontal-content-alignment="middle"
                       vertical-content-alignment="middle"
                       background={@Mods/StardewUI/Sprites/ControlBorder}
                       focusable="true"
                       click=|Activate()|>
                    <lane orientation="horizontal" vertical-content-alignment="middle">
                        <label *if={:IsActive} text="* " />
                        <label text={:Label} />
                    </lane>
                </frame>
            </lane>

            <lane orientation="horizontal" layout="stretch content">

                <frame layout="280px 580px"
                       padding="8"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <scrollable layout="stretch stretch">
                        <lane orientation="vertical" layout="stretch content">
                            <frame *repeat={:Quests}
                                   layout="stretch 48px"
                                   margin="0, 2"
                                   padding="6, 4"
                                   focusable="true"
                                   click=|Select()|>
                                <lane orientation="horizontal" vertical-content-alignment="middle">
                                    <label *if={:IsSelected} text="> " />
                                    <label text={:Title} />
                                </lane>
                            </frame>
                            <label *if={:IsEmpty} margin="8" text="No quests on this tab." />
                        </lane>
                    </scrollable>
                </frame>

                <frame layout="520px 580px"
                       margin="8, 0"
                       padding="16, 12"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <lane orientation="vertical" layout="stretch content">
                        <banner layout="stretch content"
                                margin="0, 0, 0, 12"
                                background={@Mods/StardewUI/Sprites/BannerBackground}
                                background-border-thickness="48, 0"
                                padding="12"
                                text={SelectedTitle} />
                        <label margin="0, 4" text={SelectedDescription} />
                        <label margin="0, 12, 0, 4" color="#136" text="Objective" />
                        <label margin="8, 0" text={SelectedObjective} />
                        <label margin="0, 12, 0, 4" color="#136" text="Rewards" />
                        <lane *repeat={:SelectedRewards} orientation="horizontal" margin="8, 1" vertical-content-alignment="middle">
                            <label text="- " />
                            <label text={:Summary} />
                        </lane>
                        <label margin="0, 12, 0, 4" color="#136" text="Giver" />
                        <label margin="8, 0" text={SelectedGiverDisplay} />
                        <label margin="0, 12, 0, 4" color="#136" text="Days Left" />
                        <label margin="8, 0" text={SelectedDaysLeftDisplay} />
                        <label margin="0, 12, 0, 4" color="#136" text="Source" />
                        <label margin="8, 0" text={SelectedSourceDisplay} />
                    </lane>
                </frame>

                <frame layout="220px 580px"
                       padding="12"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <lane orientation="vertical" layout="stretch content" horizontal-content-alignment="middle">
                        <label *if={SelectedIsCompleted} margin="0, 12" color="#136" text="Quest complete" />

                        <frame *if={SelectedShowActions} layout="stretch 48px" margin="0, 4" padding="8, 0"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|ShowDetailsSelected()|>
                            <label text="Details" />
                        </frame>
                        <frame *if={SelectedShowActions} layout="stretch 48px" margin="0, 4" padding="8, 0"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|PinSelected()|>
                            <label text="Pin" />
                        </frame>
                        <frame *if={SelectedShowComplete} layout="stretch 48px" margin="0, 4" padding="8, 0"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|CompleteSelected()|>
                            <label text="Complete Quest" />
                        </frame>
                        <frame *if={SelectedShowActions} layout="stretch 48px" margin="0, 4" padding="8, 0"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|WarpSelected()|>
                            <label text={SelectedWarpLabel} />
                        </frame>
                        <frame *if={SelectedShowPostpone} layout="stretch 48px" margin="0, 4" padding="8, 0"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|PostponeSelected()|>
                            <label text="Postpone (+7 days)" />
                        </frame>
                        <frame *if={SelectedShowCancel} layout="stretch 48px" margin="0, 4" padding="8, 0"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|RequestCancelSelected()|>
                            <label text="Cancel" />
                        </frame>
                    </lane>
                </frame>

            </lane>
        </lane>
    </frame>
</lane>
