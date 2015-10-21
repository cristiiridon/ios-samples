// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using System.Linq;

using CoreLocation;
using Foundation;
using MapKit;
using UIKit;

namespace HomeKitCatalog
{
	public interface IMapViewControllerDelegate
	{
		// Notifies the delegate that the `MapViewController`'s region has been updated.
		void MapViewDidUpdateRegion (CLCircularRegion region);
	}

	// A view controller which allows the selection of a circular region on a map.
	public partial class MapViewController : UIViewController, IUISearchBarDelegate, ICLLocationManagerDelegate, IMKMapViewDelegate
	{
		const string CircularRegion = "MapViewController.Region";

		// When the view loads, we'll zoom to this longitude/latitude span delta.
		const double InitialZoomDelta = 0.0015;

		// When the view loads, we'll zoom into this span.
		static readonly MKCoordinateSpan InitialZoomSpan = new MKCoordinateSpan (InitialZoomDelta, InitialZoomDelta);

		// The inverse of the percentage of the map view that should be captured in the region.
		const double MapRegionFraction = 4;

		// The size of the query region with respect to the map's zoom.
		const double RegionQueryDegreeMultiplier = 5;

		[Outlet ("overlayView")]
		public MapOverlayView OverlayView { get; set; }

		[Outlet ("searchBar")]
		public UISearchBar SearchBar { get; set; }

		[Outlet ("mapView")]
		public MKMapView MapView { get; set; }

		public IMapViewControllerDelegate Delegate { get; set; }

		public CLCircularRegion TargetRegion { get; set; }

		MKCircle circleOverlay;

		MKCircle CircleOverlay {
			get {
				return circleOverlay;
			}
			set {
				var oldValue = circleOverlay;
				circleOverlay = value;

				// Remove the old overlay (if exists)
				if (oldValue != null)
					MapView.RemoveOverlay (oldValue);

				// Add the new overlay (if exists)
				if (circleOverlay != null)
					MapView.AddOverlay (circleOverlay);
			}
		}

		readonly CLLocationManager locationManager = new CLLocationManager ();

		#region ctors

		public MapViewController (IntPtr handle)
			: base (handle)
		{
		}

		[Export ("initWithCoder:")]
		public MapViewController (NSCoder coder)
			: base (coder)
		{
		}

		#endregion

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			SearchBar.Delegate = this;
			MapView.Delegate = this;
			MapView.ShowsUserLocation = true;
			MapView.PitchEnabled = false;
			locationManager.Delegate = this;
		}

		public override void ViewDidAppear (bool animated)
		{
			base.ViewDidAppear (animated);

			locationManager.RequestWhenInUseAuthorization ();
			locationManager.RequestLocation ();

			var region = TargetRegion;
			if (region != null)
				AnnotateAndZoomToRegion (region);
		}

		public override void DidRotate (UIInterfaceOrientation fromInterfaceOrientation)
		{
			OverlayView.SetNeedsDisplay ();
		}

		[Export ("didTapSaveButton:")]
		void didTapSaveButton (UIBarButtonItem sender)
		{
			double circleDegreeDelta;
			CLLocation pointOnCircle;

			var span = MapView.Region.Span;
			if (span.LatitudeDelta > span.LongitudeDelta) {
				circleDegreeDelta = span.LongitudeDelta / MapRegionFraction;
				pointOnCircle = new CLLocation (MapView.Region.Center.Latitude, MapView.Region.Center.Longitude - circleDegreeDelta);
			} else {
				circleDegreeDelta = span.LatitudeDelta / MapRegionFraction;
				pointOnCircle = new CLLocation (MapView.Region.Center.Latitude - circleDegreeDelta, MapView.Region.Center.Longitude);
			}

			var mapCenterLocation = new CLLocation (MapView.Region.Center.Latitude, MapView.Region.Center.Longitude);
			var distance = pointOnCircle.DistanceFrom (mapCenterLocation);
			var genericRegion = new CLCircularRegion (MapView.Region.Center, distance, CircularRegion);

			circleOverlay = MKCircle.Circle (genericRegion.Center, genericRegion.Radius);
			var vcDelegate = Delegate;
			if (vcDelegate != null)
				vcDelegate.MapViewDidUpdateRegion (genericRegion);
			DismissViewController (true, null);
		}

		// Dismisses the view without notifying the delegate.
		[Export ("didTapCancelButton:")]
		void didTapCancelButton (UIBarButtonItem sender)
		{
			DismissViewController (true, null);
		}

		#region Search Bar Methods

		[Export ("searchBarSearchButtonClicked:")]
		public void SearchButtonClicked (UISearchBar searchBar)
		{
			SearchBar.ResignFirstResponder ();
			MapView.RemoveAnnotations (MapView.Annotations);
			PerformSearch ();
		}

		#endregion

		#region Location Manager Methods

		[Export ("locationManager:didUpdateLocations:")]
		public void LocationsUpdated (CLLocationManager manager, CLLocation[] locations)
		{
			CLLocation lastLocation = locations == null ? null : locations.LastOrDefault ();
			if (lastLocation == null)
				return;

			// Do not zoom to the user's location if there is already a target region.
			if (TargetRegion != null)
				return;

			var newRegion = new MKCoordinateRegion (lastLocation.Coordinate, InitialZoomSpan);
			MapView.SetRegion (newRegion, true);
		}

		[Export ("locationManager:didFailWithError:")]
		public void Failed (CLLocationManager manager, NSError error)
		{
			Console.WriteLine ("System: Location Manager Error: {0}", error);
		}

		[Export ("locationManager:didChangeAuthorizationStatus:")]
		public void AuthorizationChanged (CLLocationManager manager, CLAuthorizationStatus status)
		{
			locationManager.RequestLocation ();
		}

		#endregion

		#region Helper Methods

		void AnnotateAndZoomToRegion (CLCircularRegion region)
		{
			circleOverlay = MKCircle.Circle (region.Center, region.Radius);
			const double multiplier = MapRegionFraction;
			var mapRegion = MKCoordinateRegion.FromDistance (region.Center, region.Radius * multiplier, region.Radius * multiplier);
			MapView.SetRegion (mapRegion, false);
		}

		// Performs a natural language search for locations in the map's region that match the `searchBar`'s text.
		void PerformSearch ()
		{
			var request = new MKLocalSearchRequest ();
			request.NaturalLanguageQuery = SearchBar.Text;
			const double multiplier = RegionQueryDegreeMultiplier;
			var querySpan = new MKCoordinateSpan (MapView.Region.Span.LatitudeDelta * multiplier, MapView.Region.Span.LongitudeDelta * multiplier);
			request.Region = new MKCoordinateRegion (MapView.Region.Center, querySpan);

			var search = new MKLocalSearch (request);
			var matchingItems = new List<MKMapItem> ();

			search.Start ((response, error) => {
				MKMapItem[] mapItems = null;
				if (response != null)
					mapItems = response.MapItems ?? new MKMapItem[0];

				foreach (var item in mapItems) {
					matchingItems.Add (item);
					var annotation = new MKPointAnnotation ();
					annotation.Coordinate = item.Placemark.Coordinate;
					annotation.Title = item.Name;
					MapView.AddAnnotation (annotation);
				}
			});
		}

		[Export ("mapView:rendererForOverlay:")]
		public MKOverlayRenderer OverlayRenderer (MKMapView mapView, IMKOverlay overlay)
		{
			return new MKOverlayPathRenderer (overlay) {
				FillColor = UIColor.Blue.ColorWithAlpha (0.2f),
				StrokeColor = UIColor.Black,
				LineWidth = 2
			};
		}

		#endregion
	}
}