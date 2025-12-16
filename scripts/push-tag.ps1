<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <!-- Default: disabled. Set /p:TriggerGitHubRelease=true to enable in a build -->
    <TriggerGitHubRelease Condition="'$(TriggerGitHubRelease)' == ''">false</TriggerGitHubRelease>
  </PropertyGroup>

  <Target Name="TriggerGitHubRelease" AfterTargets="Build"
          Condition="'$(Configuration)' == 'FullRelease' and '$(TriggerGitHubRelease)' == 'true'">
    <Message Text="TriggerGitHubRelease=true — running: $(MSBuildProjectDirectory)\..\scripts\push-tag.ps1 -AutoCommit" Importance="high" />
    <Exec Command="powershell -NoProfile -ExecutionPolicy Bypass -File &quot;$(MSBuildProjectDirectory)\..\scripts\push-tag.ps1&quot; -AutoCommit" />
  </Target>
</Project>