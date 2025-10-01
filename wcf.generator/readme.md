***

### Jak to wykorzystać?

1. Utwórz osobny projekt klasy biblioteki (.NET Standard lub .NET 8) dla Source Generatora.
2. Dodaj do konstrukcji projektu pakiety `Microsoft.CodeAnalysis.CSharp` i `Microsoft.CodeAnalysis.Analyzers` (wersje kompatybilne z Roslyn dla .NET 8).
3. Skopiuj powyższy kod do klasy generatora.
4. W kliencie dodaj referencję do projektu generatora jako `Analyzer` (np. w pliku `.csproj`).
5. Przy budowie projektu klienta zostaną wygenerowane „czyste” klasy bez atrybutów.
6. Wystarczy używać wygenerowanych plików zamiast oryginalnych z atrybutami.

***

### Uwagi

- Ten przykładowy generator jest bardzo uproszczony, wymaga rozszerzenia obsługi na atrybuty na metodach i właściwościach.
- Można rozszerzyć o kontrolę namespace, generowanie interfejsów i innych struktur.
- Generator działa tylko w trakcie budowy i nie zmienia oryginalnych plików.
- To rozwiązanie ułatwia utrzymanie jednego kodu kontraktów dla wielu targetów.


# jak taki generator podpiąć automatycznie podzas kompilacji / publikacji / tworzenia nugeta?

Aby automatycznie podpiąć Roslyn Source Generator do procesu kompilacji, publikacji oraz tworzenia paczki NuGet, wykonaj następujące kroki:

### 1. W projekcie generatora

- Projekt generatora powinien być projektem typu **Class Library** (np. netstandard2.0 lub net8.0) i zawierać pakiety:
    - `Microsoft.CodeAnalysis.CSharp`
    - `Microsoft.CodeAnalysis.Analyzers` (opcjonalnie)
- W pliku `.csproj` generatora powinno być:

```xml
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework> <!-- lub net8.0 -->
  <EmitCompilerGeneratedFiles>false</EmitCompilerGeneratedFiles>
  <AssemblyName>RemoveWcfAttributesGenerator</AssemblyName>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.7.0" PrivateAssets="all"/>
  <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.3" PrivateAssets="all"/>
</ItemGroup>
```


### 2. Publikacja generatora jako NuGet

- Wygeneruj pakiet NuGet z projektu generatora:

```
dotnet pack -c Release
```

- Opublikuj lub lokalnie zainstaluj ten pakiet.


### 3. Użycie generatora w projekcie klienta

- W projekcie klienta, w pliku `.csproj`, dodaj referencję do generatora z atrybutem `Analyzer`:

```xml
<ItemGroup>
  <PackageReference Include="RemoveWcfAttributesGenerator" Version="1.0.0" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

- Ważne:
    - `OutputItemType="Analyzer"` mówi MSBuild, że pakiet to źródło analiz i generatorów.
    - `ReferenceOutputAssembly="false"` oznacza, że nie kompilujemy generatora do aplikacji, tylko uruchamiamy go przy budowaniu.


### 4. Proces kompilacji i publikacji

- Przy budowie klienta, generator zostanie automatycznie uruchomiony i wygeneruje dodatkowe pliki ze zmodyfikowanym (odchudzonym) kodem.
- Pliki wygenerowane są w pamięci przez kompilator i dostępne w trakcie build i dla Intellisense.
- Możesz konfigurować generator, żeby generował kody, które później są kompilowane do finalnej aplikacji lub NuGeta.


### 5. Dla tworzenia NuGeta

- Wygenerowane pliki są częścią projektu i będą uwzględniane w finalnym pakiecie, jeśli masz domyślną konfigurację.

```
- Jeśli chcesz, możesz jawnie kontrolować, które pliki trafiają do NuGeta przez użycie `<Compile Remove="...">` lub `<None>` w `.csproj`, ale zwykle jest to zbędne.
```


***

### Podsumowanie

| Krok | Co zrobić |
| :-- | :-- |
| 1. Projekt generatora | Stwórz Class Library z Roslyn |
| 2. Publikacja | Wygeneruj i opublikuj NuGet generatora |
| 3. Klient | Dodaj referencję do generatora jako `<PackageReference>` z `OutputItemType="Analyzer"` |
| 4. Budowa | Generator wykona się automatycznie |
| 5. NuGet | Wygenerowane pliki włączane są do paczki automatycznie |


***


