# SimTrackerV2 — V3 Build Log

## 2026-04-14

### Live Map Feature (full implementation)

**New files:**
- `SimCrewOps.Hosting/Models/LiveFlight.cs` — API response model for `/api/tracker/live-flights`
- `SimCrewOps.Hosting/Hosting/LiveMapService.cs` — polls fleet positions every 5s via PeriodicTimer, raises `PositionsUpdated` event, Bearer token auth
- `SimCrewOps.App.Wpf/Infrastructure/MapProjection.cs` — equirectangular lat/lon → canvas pixel projection with clamping
- `SimCrewOps.App.Wpf/Models/PlaneMarkerModel.cs` — per-flight display model with `FromLiveFlight` factory
- `SimCrewOps.App.Wpf/Views/LiveMapCanvas.cs` — custom WPF FrameworkElement; renders lat/lon grid, continent silhouettes, rotated plane icons, and callsign/FL labels via DrawingContext. Binds to `LiveFlights` dependency property. Orange = own flight, steel blue = others.

**Modified files:**
- `TrackerServiceStack.cs` — added `LiveMapService?` property
- `TrackerServiceFactory.cs` — creates `LiveMapService` when API token is present; added to both stack return paths
- `TrackerShellBootstrapResult.cs` — added `LiveMapService?` to thread service through to ViewModel
- `TrackerShellBootstrapper.cs` — passes `serviceStack.LiveMapService` into bootstrap result
- `NavPage.cs` — added `LiveMap` value (between Dashboard and Review)
- `MainWindowViewModel.cs` — `ShowLiveMapCommand`, `IsLiveMapVisible`, `LiveMapNavUnderlineVisibility`, `LiveFlights` property, `OnLiveMapPositionsUpdated` handler; polling starts when tab is selected, stops on tab switch
- `MainWindow.xaml` — "Live Map" nav button, `xmlns:local` namespace, live map panel with `LiveMapCanvas` bound to `LiveFlights`

**UI mockups (committed alongside):**
- `docs/mockups/simcrewops-trackerv2-mockup.html` — approved light mode mockup with custom airplane SVG icon and SimCrewOps logos
- `docs/mockups/simcrewops-trackerv2-mockup-dark.html` — approved dark mode mockup

**Design decisions:**
- Canvas uses FrameworkElement + OnRender rather than XAML ItemsControl for performance (no UI virtualization overhead on a frequently-updating collection)
- LiveMapService starts/stops with tab visibility to avoid unnecessary API polling
- Mapbox deferred to next iteration (will use WebView2 + Mapbox GL JS dark style)
