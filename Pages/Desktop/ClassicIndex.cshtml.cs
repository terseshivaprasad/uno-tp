using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UnoTp.Pages.Desktop;

/// <summary>
/// The old E-Sarathi console (WA_FD_ESARATHI_CONSOLE/Default), the page a partner
/// lands on before picking an application. It holds no data of its own: the
/// profile comes from <see cref="UnoTp.Models.MockData"/> and the tiles are the
/// two applications this entity is provisioned for.
/// </summary>
public class ClassicIndexModel : PageModel
{
    public void OnGet()
    {
    }
}
