// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Plugins
{
	/// <summary>
	/// Defines the convention that plugin assemblies must follow for discovery.
	/// Each plugin DLL must contain a public static class named "PluginEntry"
	/// with a static method "CreatePlugins()" returning IEnumerable&lt;IPlugin&gt;.
	/// </summary>
	/// <example>
	/// // In your plugin assembly:
	/// public static class PluginEntry
	/// {
	///     public static IEnumerable&lt;IPlugin&gt; CreatePlugins()
	///         =&gt; [new MyPlugin(), new AnotherPlugin()];
	/// }
	/// </example>
	public static class PluginEntryConvention
	{
		/// <summary>
		/// The expected class name that plugin assemblies must define.
		/// </summary>
		public const string EntryClassName = "PluginEntry";

		/// <summary>
		/// The expected method name on the entry class.
		/// </summary>
		public const string FactoryMethodName = "CreatePlugins";
	}
}
