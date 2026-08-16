// <copyright file="LinksPopup.cs" company="MTVirux">
// Copyright (c) MTVirux. All rights reserved.
// </copyright>

namespace MarketTerror.GUI.Components
{
  using System;
  using System.Numerics;
  using Dalamud.Bindings.ImGui;
  using MarketTerror.Helpers;

  /// <summary>
  /// The popup opened by the heart button in the title bar: the Universalis status link and the repository link.
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
        if (ImGui.Button("Data provided by Universalis", buttonSize))
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
        if (ImGui.Button("Universalis API seems down", buttonSize))
        {
          Utilities.OpenBrowser("https://status.universalis.app");
        }
      }

      if (ImGui.Button("SeaOfTerror Repo", buttonSize))
      {
        Utilities.OpenBrowser("https://github.com/MTVirux/SeaOfTerror");
      }

      ImGui.PopStyleColor(3);
      ImGui.EndPopup();
    }
  }
}
