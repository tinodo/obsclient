// Customizes the DocFX modern template: the colour theme follows the setting of the visitor's system,
// and the header gets links to the source and to the package.
export default {
  defaultTheme: 'auto',
  iconLinks: [
    {
      icon: 'github',
      href: 'https://github.com/tinodo/obsclient',
      title: 'GitHub'
    },
    {
      icon: 'box-seam',
      href: 'https://www.nuget.org/packages/OBSClient',
      title: 'NuGet'
    }
  ]
}
