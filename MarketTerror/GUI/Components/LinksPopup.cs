// <copyright file="LinksPopup.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using Dalamud.Interface;
  using MarketTerror.Helpers;

  /// <summary>
  /// The popup opened by the heart button in the title bar: the Universalis status link, the FFXIVMT site, the repository link and the issue tracker.
  /// </summary>
  public sealed class LinksPopup
  {
    private const string PopupId = "marketTerrorLinks";

    private readonly MarketBoardContext context;

    private bool openRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinksPopup"/> class.
    /// </summary>
    /// <param name="context">The shared market board state.</param>
    public LinksPopup(MarketBoardContext context)
    {
      this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Asks for the popup to be opened on the next draw.
    /// </summary>
    /// <remarks>
    /// The title bar button is clicked before the window body is drawn, so the open has to be deferred.
    /// </remarks>
    public void Open()
    {
      this.openRequested = true;
    }

    /// <summary>
    /// Draws the popup. Call this at the end of the window body.
    /// </summary>
    public void Draw()
    {
      if (this.openRequested)
      {
        this.openRequested = false;
        ImGui.OpenPopup(PopupId);
      }

      if (!ImGui.BeginPopup(PopupId))
      {
        return;
      }

      var scale = ImGui.GetIO().FontGlobalScale;
      var buttonSize = new Vector2(200 * scale, 0);

      ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ScrollbarGrab));
      ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.ScrollbarGrabHovered));
      ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.ScrollbarGrabActive));

      if (this.context.MarketData.IsUniversalisUp != false)
      {
        if (LinkButton("universalis", "Universalis", buttonSize))
        {
          var universalisUrl = "https://universalis.app";
          var selectedItem = this.context.SelectedItem;

          if (selectedItem.HasValue)
          {
            universalisUrl += $"/market/{selectedItem.Value.RowId}";
          }

          Utilities.OpenBrowser(universalisUrl);
        }
      }
      else
      {
        if (LinkButton("universalisStatus", "Universalis", buttonSize))
        {
          Utilities.OpenBrowser("https://status.universalis.app");
        }

        if (ImGui.IsItemHovered())
        {
          ImGui.SetTooltip("The Universalis API seems down");
        }
      }

      if (LinkButton("ffxivmt", "FFXIVMT", buttonSize))
      {
        Utilities.OpenBrowser("https://mtvirux.app");
      }

      if (LinkButton("repo", "SeaOfTerror", buttonSize))
      {
        Utilities.OpenBrowser("https://github.com/MTVirux/SeaOfTerror");
      }

      if (LinkButton("issue", "Report an issue", buttonSize, FontAwesomeIcon.Wrench))
      {
        Utilities.OpenBrowser("https://github.com/MTVirux/SeaOfTerror/issues/new");
      }

      ImGui.PopStyleColor(3);
      ImGui.EndPopup();
    }

    private static bool LinkButton(string id, string label, Vector2 size, FontAwesomeIcon icon = FontAwesomeIcon.Heart)
    {
      var clicked = ImGui.Button($"##{id}", size);

      var min = ImGui.GetItemRectMin();
      var height = ImGui.GetItemRectSize().Y;
      var padding = ImGui.GetStyle().FramePadding.X;
      var color = ImGui.GetColorU32(ImGuiCol.Text);
      var drawList = ImGui.GetWindowDrawList();

      var glyph = $"{(char)icon}";
      ImGui.PushFont(UiBuilder.IconFont);
      var glyphSize = ImGui.CalcTextSize(glyph);
      drawList.AddText(new Vector2(min.X + padding, min.Y + ((height - glyphSize.Y) / 2f)), color, glyph);
      ImGui.PopFont();

      var labelSize = ImGui.CalcTextSize(label);
      drawList.AddText(new Vector2(min.X + padding + glyphSize.X + padding, min.Y + ((height - labelSize.Y) / 2f)), color, label);

      return clicked;
    }
  }
}
