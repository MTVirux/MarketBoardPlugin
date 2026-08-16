// <copyright file="StatusFooter.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using MarketTerror.Helpers;

  /// <summary>
  /// The bottom bar of the market board window: the Universalis status button and the repository link.
  /// </summary>
  public sealed class StatusFooter
  {
    private readonly MarketBoardContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusFooter"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public StatusFooter(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Draws the footer at the bottom of the current child window.
    /// </summary>
    public void Draw()
    {
      var scale = ImGui.GetIO().FontGlobalScale;

      ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight());

      if (this.context.MarketData.IsUniversalisUp)
      {
        var buttonColor = 0x002ba040u;

        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | buttonColor);

        if (ImGui.Button("Data provided by Universalis"))
        {
          var universalisUrl = "https://universalis.app";
          var selectedItem = this.context.SelectedItem;

          if (selectedItem.HasValue)
          {
            universalisUrl += $"/market/{selectedItem.Value.RowId}";
          }

          Utilities.OpenBrowser(universalisUrl);
        }

        ImGui.PopStyleColor(3);
      }
      else
      {
        var buttonColor = 0x005345e6u;

        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | buttonColor);

        if (ImGui.Button("Universalis API seems down"))
        {
          Utilities.OpenBrowser("https://status.universalis.app");
        }

        ImGui.PopStyleColor(3);
      }

      ImGui.SameLine(ImGui.GetContentRegionAvail().X - (120 * scale));

      if (!this.context.Config.KofiHidden)
      {
        var buttonColor = 0x005E5BFFu;

        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | buttonColor);

        if (ImGui.Button("SeaOfTerror Repo", new Vector2(120, 0)))
        {
          Utilities.OpenBrowser("https://github.com/MTVirux/SeaOfTerror");
        }

        ImGui.PopStyleColor(3);
      }
      else
      {
        ImGui.Dummy(new Vector2(120, 24));
      }
    }
  }
}
