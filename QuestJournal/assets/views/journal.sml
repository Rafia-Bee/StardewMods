<lane orientation="vertical"
      horizontal-content-alignment="middle"
      vertical-content-alignment="middle">
    <frame layout="1100px 720px"
           background={@Mods/StardewUI/Sprites/MenuBackground}
           border={@Mods/StardewUI/Sprites/MenuBorder}
           border-thickness="36, 36, 40, 36"
           padding="20, 12"
           horizontal-content-alignment="middle">
        <lane orientation="vertical" layout="stretch content" horizontal-content-alignment="middle">

            <lane orientation="horizontal" margin="0, 0, 0, 8">
                <frame *repeat={:Tabs}
                       layout="140px 76px"
                       margin="0, 0, 8, 0"
                       padding="12, 8"
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

            <lane orientation="horizontal" layout="content 580px" vertical-content-alignment="start">

                <frame layout="240px 580px"
                       padding="20"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <scrollable layout="stretch stretch"
                                scrollbar-visibility="visible"
                                scrollbar-margin="-44, 16, 0, 16"
                                scrollbar-track-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-up-sprite={@Mods/RafiaBee.QuestJournal/Sprites/up_arrow:Arrow}
                                scrollbar-down-sprite={@Mods/RafiaBee.QuestJournal/Sprites/down_arrow:Arrow}
                                scrollbar-thumb-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}>
                        <lane orientation="vertical" layout="stretch content" margin="0, 0, 40, 0">
                            <lane *repeat={:Quests} orientation="vertical" layout="stretch content">
                                <frame layout="stretch content"
                                       padding="10, 8"
                                       background={@Mods/RafiaBee.QuestJournal/Sprites/highlight:Highlight}
                                       background-tint={RowTint}
                                       focusable="true"
                                       click=|Select()|
                                       pointer-enter=|HoverEnter()|
                                       pointer-leave=|HoverLeave()|>
                                    <label bold={IsSelected} text={:Title} />
                                </frame>
                                <frame *if={ShowDivider}
                                       layout="stretch 3px"
                                       margin="6, 4"
                                       background={@Mods/RafiaBee.QuestJournal/Sprites/highlight:Highlight}
                                       background-tint={DividerTint} />
                            </lane>
                            <label *if={:IsEmpty} margin="8" text="No quests on this tab." />
                        </lane>
                    </scrollable>
                </frame>

                <frame layout="484px 580px"
                       margin="8, 0"
                       padding="20"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <scrollable layout="stretch stretch"
                                scrollbar-visibility="visible"
                                scrollbar-margin="-44, 16, 0, 16"
                                scrollbar-track-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-up-sprite={@Mods/RafiaBee.QuestJournal/Sprites/up_arrow:Arrow}
                                scrollbar-down-sprite={@Mods/RafiaBee.QuestJournal/Sprites/down_arrow:Arrow}
                                scrollbar-thumb-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}>
                        <lane orientation="vertical" layout="stretch content" margin="0, 0, 40, 0">
                            <frame layout="stretch content"
                                   margin="0, 0, 0, 12"
                                   background={@Mods/StardewUI/Sprites/BannerBackground}
                                   border-thickness="48, 12"
                                   padding="0, 4"
                                   horizontal-content-alignment="middle"
                                   vertical-content-alignment="middle">
                                <label layout="stretch content"
                                       bold="true"
                                       horizontal-alignment="middle"
                                       max-lines="2"
                                       text={SelectedTitle} />
                            </frame>
                            <label margin="0, 4" text={SelectedDescription} />
                            <label *if={SelectedShowObjective} margin="0, 12, 0, 4" color={HeaderColor} text="Objective" />
                            <label *if={SelectedShowObjective} margin="8, 0" text={SelectedObjective} />
                            <label *if={SelectedHasSteps} margin="0, 12, 0, 4" color={HeaderColor} text="Steps" />
                            <label *repeat={:SelectedSteps} margin="8, 1" text={:RowText} />
                            <label margin="0, 12, 0, 4" color={HeaderColor} text="Rewards" />
                            <lane *repeat={:SelectedRewards} orientation="horizontal" margin="8, 1" vertical-content-alignment="middle">
                                <label text="- " />
                                <label text={:Summary} />
                            </lane>
                            <label margin="0, 12, 0, 4" color={HeaderColor} text="Giver" />
                            <label margin="8, 0" text={SelectedGiverDisplay} />
                            <label margin="0, 12, 0, 4" color={HeaderColor} text="Days Left" />
                            <label margin="8, 0" text={SelectedDaysLeftDisplay} />
                            <label margin="0, 12, 0, 4" color={HeaderColor} text="Source" />
                            <label margin="8, 0" text={SelectedSourceDisplay} />
                        </lane>
                    </scrollable>
                </frame>

                <frame layout="236px 580px"
                       padding="20"
                       background={@Mods/StardewUI/Sprites/ControlBorder}>
                    <scrollable layout="stretch stretch"
                                scrollbar-visibility="hidden"
                                scrollbar-track-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-up-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-down-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}
                                scrollbar-thumb-sprite={@Mods/RafiaBee.QuestJournal/Sprites/blank:Blank}>
                        <lane orientation="vertical" layout="stretch 540px" horizontal-content-alignment="middle">
                        <label *if={SelectedIsCompleted} margin="0, 12" color={HeaderColor} text="Quest complete" />

                        <frame *if={SelectedShowActions} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|ShowDetailsSelected()|>
                            <label text="Details" />
                        </frame>
                        <frame *if={SelectedShowActions} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|PinSelected()|>
                            <label text="Pin" />
                        </frame>
                        <frame *if={SelectedShowComplete} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|CompleteSelected()|>
                            <label text="Complete Quest" />
                        </frame>
                        <frame *if={SelectedShowActions} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|WarpSelected()|>
                            <label text={SelectedWarpLabel} />
                        </frame>
                        <frame *if={SelectedShowPostpone} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|PostponeSelected()|>
                            <label text="Postpone (+7 days)" />
                        </frame>
                        <frame *if={SelectedShowCancel} layout="stretch content" margin="0, 4" padding="8, 14"
                               background={@Mods/StardewUI/Sprites/ButtonLight}
                               horizontal-content-alignment="middle"
                               vertical-content-alignment="middle"
                               focusable="true"
                               click=|RequestCancelSelected()|>
                            <label text="Cancel" />
                        </frame>
                        </lane>
                    </scrollable>
                </frame>

            </lane>
        </lane>
    </frame>
</lane>
