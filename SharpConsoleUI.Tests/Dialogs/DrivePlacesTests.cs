// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Dialogs;
using Xunit;

namespace SharpConsoleUI.Tests.Dialogs;

public class DrivePlacesTests
{
	// A synthetic drive record so tests never depend on the host mount table.
	private static DrivePlaces.RawDrive Raw(string root, string label, string type, bool ready = true)
		=> new DrivePlaces.RawDrive(root, label, type, ready, 0, 0);

	[Fact]
	public void Linux_KeepsRootAndMedia_ExcludesVirtualFs()
	{
		var drives = new List<DrivePlaces.RawDrive>
		{
			Raw("/", "", "Fixed"),
			Raw("/proc", "", "Ram"),
			Raw("/sys", "", "Ram"),
			Raw("/snap/btop/998", "", "Fixed"),
			Raw("/var/lib/docker/overlay2/abc/merged", "", "Unknown", ready: false),
			Raw("/boot/efi", "", "Fixed"),
			Raw("/media/usb", "USB DRIVE", "Removable"),
			Raw("/mnt/nas", "", "Network"),
		};

		var places = DrivePlaces.GetPlaces(currentPath: "/home/nick", drives: drives, isWindows: false, homePath: "/home/nick");
		var paths = places.Select(p => p.Path).ToList();

		Assert.Contains("/", paths);
		Assert.Contains("/media/usb", paths);
		Assert.Contains("/mnt/nas", paths);
		Assert.DoesNotContain("/proc", paths);
		Assert.DoesNotContain("/sys", paths);
		Assert.DoesNotContain("/snap/btop/998", paths);
		Assert.DoesNotContain("/var/lib/docker/overlay2/abc/merged", paths);
		Assert.DoesNotContain("/boot/efi", paths);
	}

	[Fact]
	public void Windows_IncludesAllReadyDrives()
	{
		var drives = new List<DrivePlaces.RawDrive>
		{
			Raw("C:\\", "Local Disk", "Fixed"),
			Raw("D:\\", "Data", "Fixed"),
			Raw("E:\\", "USB DRIVE", "Removable"),
			Raw("Z:\\", "", "Network"),
		};

		var places = DrivePlaces.GetPlaces("C:\\Users\\nick", drives, isWindows: true, homePath: "C:\\Users\\nick");
		var paths = places.Select(p => p.Path).ToList();

		Assert.Contains("C:\\", paths);
		Assert.Contains("D:\\", paths);
		Assert.Contains("E:\\", paths);
		Assert.Contains("Z:\\", paths);
	}

	[Fact]
	public void HomeEntry_AddedOnAllPlatforms()
	{
		var drives = new List<DrivePlaces.RawDrive> { Raw("/", "", "Fixed") };
		var places = DrivePlaces.GetPlaces("/", drives, isWindows: false, homePath: "/home/nick");
		Assert.Contains(places, p => p.DisplayName == "Home" && p.Path == "/home/nick");
	}

	[Fact]
	public void CurrentPlace_IsLongestPrefixMatch()
	{
		var drives = new List<DrivePlaces.RawDrive>
		{
			Raw("/", "", "Fixed"),
			Raw("/media/usb", "USB", "Removable"),
		};
		// /media/usb/photos should resolve to /media/usb, NOT / .
		var places = DrivePlaces.GetPlaces("/media/usb/photos", drives, isWindows: false, homePath: null);
		var current = places.Single(p => p.IsCurrent);
		Assert.Equal("/media/usb", current.Path);
	}

	[Fact]
	public void Icons_MatchDriveType()
	{
		var drives = new List<DrivePlaces.RawDrive>
		{
			Raw("/media/usb", "USB", "Removable"),
			Raw("/mnt/nas", "", "Network"),
			Raw("/media/cd", "", "CDRom"),
			Raw("/", "", "Fixed"),
		};
		var places = DrivePlaces.GetPlaces("/", drives, isWindows: false, homePath: null);
		Assert.Equal("🔌", places.Single(p => p.Path == "/media/usb").Icon);
		Assert.Equal("🌐", places.Single(p => p.Path == "/mnt/nas").Icon);
		Assert.Equal("💿", places.Single(p => p.Path == "/media/cd").Icon);
		Assert.Equal("💾", places.Single(p => p.Path == "/").Icon);
	}

	[Fact]
	public void DuplicateMountRoots_AreDeduplicated()
	{
		// The OS can report the same network mount twice.
		var drives = new List<DrivePlaces.RawDrive>
		{
			Raw("/", "", "Fixed"),
			Raw("/mnt/nas", "/mnt/nas", "Network"),
			Raw("/mnt/nas", "/mnt/nas", "Network"),
		};
		var places = DrivePlaces.GetPlaces("/", drives, isWindows: false, homePath: null);
		Assert.Single(places.Where(p => p.Path == "/mnt/nas"));
	}

	[Fact]
	public void Detail_DoesNotRepeatPathWhenLabelEqualsRoot()
	{
		var drives = new List<DrivePlaces.RawDrive>
		{
			new DrivePlaces.RawDrive("/mnt/nas", "/mnt/nas", "Network", true, 100, 50),
		};
		var places = DrivePlaces.GetPlaces("/", drives, isWindows: false, homePath: null);
		var nas = places.Single(p => p.Path == "/mnt/nas");
		// Detail should be "Network · ... free", not "/mnt/nas · ... free".
		Assert.StartsWith("Network", nas.Detail);
		Assert.DoesNotContain("/mnt/nas", nas.Detail);
	}

	[Fact]
	public void RealEnumeration_IsNonEmpty_AndExcludesBlacklist()
	{
		// CI-SAFE: asserts invariants only, never specific removable drives,
		// because the host/CI mount table is non-deterministic.
		var places = DrivePlaces.GetPlaces(currentPath: System.Environment.CurrentDirectory);
		Assert.NotEmpty(places);
		foreach (var p in places)
		{
			Assert.DoesNotContain("/proc", p.Path);
			Assert.DoesNotContain("/sys/", p.Path);
			Assert.DoesNotContain("/snap/", p.Path);
			Assert.DoesNotContain("/var/lib/docker", p.Path);
		}
	}

	[Theory]
	[InlineData("C:\\Users\\nick\\Documents", "C:", "\\Users\\nick\\Documents")]
	[InlineData("/media/usb/photos", "/media/usb", "/photos")]
	[InlineData("/home/nick", "/", "home/nick")]
	[InlineData("/", "/", "")]
	public void SplitDriveSegment_SeparatesRootFromRemainder(string path, string expectedSeg, string expectedRest)
	{
		var (seg, rest) = DrivePlaces.SplitDriveSegment(path);
		Assert.Equal(expectedSeg, seg);
		Assert.Equal(expectedRest, rest);
	}
}
