// Edytor kodu C# dla aplikacji GamePlayer: projekty, zakładki plików, błędy i podpowiedzi.
// Komunikacja z C# (EditorBridge):
//   JS -> C#: Android.state(), openProject, createProject, deleteProject, createFile, renameFile, deleteFile,
//             save(project, file, code), compile(project), complete(id, project, file, code, pos)
//             setHelpOpen(bool)
//   C# -> JS: window.onCompileResult(json), window.onCompletions(id, json), window.onResumed(), window.closeHelp()
import { EditorView, basicSetup } from "codemirror";
import { EditorState } from "@codemirror/state";
import { keymap } from "@codemirror/view";
import { indentWithTab } from "@codemirror/commands";
import { StreamLanguage, indentUnit, HighlightStyle, syntaxHighlighting } from "@codemirror/language";
import { csharp } from "@codemirror/legacy-modes/mode/clike";
import { lintGutter, setDiagnostics } from "@codemirror/lint";
import { oneDarkTheme } from "@codemirror/theme-one-dark";
import { tags as t } from "@lezer/highlight";

const bridge = window.Android;
const status = document.getElementById("status");
const playButton = document.getElementById("play");
const projectButton = document.getElementById("project");
const tabs = document.getElementById("tabs");
const errorsPanel = document.getElementById("errors");
const modal = document.getElementById("modal");
const dialogBox = document.getElementById("dialog");

// Kolory składni bez czerwieni (One Dark ma czerwone zmienne i właściwości).
const highlight = HighlightStyle.define([
  { tag: [t.keyword, t.modifier, t.operatorKeyword, t.controlKeyword], color: "#c678dd" },
  { tag: [t.typeName, t.className, t.namespace], color: "#e5c07b" },
  { tag: [t.function(t.variableName), t.function(t.propertyName)], color: "#61afef" },
  { tag: [t.variableName, t.propertyName, t.name], color: "#d7dae0" },
  { tag: [t.definition(t.variableName)], color: "#9fd3ff" },
  { tag: [t.string, t.special(t.string), t.character], color: "#98c379" },
  { tag: [t.number, t.bool, t.null, t.atom], color: "#d19a66" },
  { tag: [t.operator, t.punctuation, t.bracket], color: "#abb2bf" },
  { tag: [t.comment, t.lineComment, t.blockComment], color: "#7f848e", fontStyle: "italic" },
  { tag: [t.meta, t.annotation], color: "#56b6c2" },
]);

// --- Stan projektu ---
let project = "";
let projects = [];
let current = null; // nazwa otwartego pliku
const files = new Map(); // nazwa -> { state: EditorState, saved: tekst na dysku, hasError }

// --- IntelliSense: podpowiedzi liczy Roslyn po stronie C# ---
let nextRequestId = 1;
const pendingCompletions = new Map();

window.onCompletions = (id, result) => {
  const resolve = pendingCompletions.get(id);
  if (!resolve) return;
  pendingCompletions.delete(id);
  resolve(result);
};

function roslynCompletions(context) {
  if (!bridge) return null;
  const word = context.matchBefore(/\w*/);
  const afterDot = context.state.sliceDoc(word.from - 1, word.from) === ".";
  if (!context.explicit && !afterDot && word.from === word.to) return null;

  const id = nextRequestId++;
  return new Promise((resolve) => {
    pendingCompletions.set(id, resolve);
    bridge.complete(id, project, current, context.state.doc.toString(), context.pos);
  }).then((result) =>
    result && result.options.length
      ? {
          from: result.from,
          options: result.options.map((o) => ({ label: o.label, type: o.type, detail: o.detail ?? undefined })),
          validFor: /^\w*$/,
        }
      : null
  );
}

// --- Autozapis ---
let saveTimer = 0;
function scheduleSave() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(saveNow, 1000);
}
// Pliki w tle są zapisane, bo zapisujemy przy każdym przełączeniu, więc wystarczy otwarty.
function saveNow() {
  clearTimeout(saveTimer);
  const entry = files.get(current);
  if (!bridge || !entry) return;
  const text = view.state.doc.toString();
  if (text === entry.saved) return;
  entry.saved = text;
  setStatus(bridge.save(project, current, text), "muted");
}
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState === "hidden") saveNow();
});

const extensions = [
  basicSetup,
  keymap.of([indentWithTab]),
  indentUnit.of("    "),
  StreamLanguage.define(csharp),
  EditorState.languageData.of(() => [{ autocomplete: roslynCompletions }]),
  lintGutter(),
  oneDarkTheme,
  syntaxHighlighting(highlight),
  EditorView.updateListener.of((update) => {
    if (update.docChanged) scheduleSave();
  }),
  EditorView.theme({ "&": { height: "100%" }, ".cm-scroller": { fontSize: "14px" } }),
];

const createState = (text) => EditorState.create({ doc: text, extensions });

const view = new EditorView({ parent: document.getElementById("editor"), state: createState("") });

function setStatus(text, kind) {
  status.textContent = text;
  status.className = kind || "";
}

// --- Projekt i zakładki ---

// Wczytuje stan z dysku. Niezmienione pliki zachowują historię cofania i pozycję kursora.
function applyState(state, preferredFile) {
  if (state.project !== project) {
    files.clear();
    current = null;
    showErrors([]);
  } else if (files.has(current)) {
    files.get(current).state = view.state;
  }

  project = state.project;
  projects = state.projects;

  const names = new Set(state.files.map((f) => f.name));
  for (const name of [...files.keys()]) if (!names.has(name)) files.delete(name);
  for (const file of state.files) {
    const entry = files.get(file.name);
    if (!entry) files.set(file.name, { state: createState(file.text), saved: file.text, hasError: false });
    else if (entry.saved !== file.text) {
      Object.assign(entry, { state: createState(file.text), saved: file.text, hasError: false });
      showErrors([]); // pozycje błędów dotyczą starego tekstu
    }
  }

  current = [preferredFile, current].find((name) => files.has(name)) ?? state.files[0].name;
  view.setState(files.get(current).state);
  renderTabs();
}

function loadState() {
  return bridge ? JSON.parse(bridge.state()) : { project: "demo", projects: ["demo"], files: [{ name: "Game.cs", text: "// brak mostka Android\n" }] };
}

function renderTabs() {
  projectButton.textContent = project + " ▾";
  tabs.replaceChildren();
  for (const [name, entry] of files) {
    const tab = document.createElement("button");
    tab.textContent = name;
    tab.className = [name === current ? "active" : "", entry.hasError ? "error" : ""].join(" ");
    tab.addEventListener("click", () => (name === current ? fileMenu() : openFile(name)));
    tabs.append(tab);
    if (name === current) tab.scrollIntoView({ inline: "nearest", block: "nearest" });
  }
  const add = document.createElement("button");
  add.textContent = "+";
  add.addEventListener("click", newFile);
  tabs.append(add);
}

function openFile(name) {
  if (name === current) return;
  saveNow();
  files.get(current).state = view.state;
  current = name;
  view.setState(files.get(name).state);
  renderTabs();
}

// --- Okna dialogowe ---

// Pokazuje okno i zwraca wartość wybranej akcji. Z polem tekstowym wartość "ok" zastępuje wpisany tekst.
function dialog({ title, input, actions }) {
  return new Promise((resolve) => {
    const close = (value) => {
      modal.hidden = true;
      dialogBox.replaceChildren();
      resolve(value);
    };

    const heading = document.createElement("h2");
    heading.textContent = title;
    dialogBox.replaceChildren(heading);

    let field = null;
    if (input !== undefined) {
      field = document.createElement("input");
      field.value = input;
      field.autocapitalize = "off";
      dialogBox.append(field);
    }

    for (const action of actions) {
      const button = document.createElement("button");
      button.textContent = action.label;
      button.className = action.kind || "";
      button.addEventListener("click", () => close(action.value === "ok" && field ? field.value : action.value));
      dialogBox.append(button);
    }

    modal.hidden = false;
    field?.focus();
  });
}

const askText = (title, value = "") =>
  dialog({
    title,
    input: value,
    actions: [
      { label: "OK", value: "ok", kind: "primary" },
      { label: "Anuluj", value: null },
    ],
  });

const confirmDelete = (title) =>
  dialog({
    title,
    actions: [
      { label: "Usuń", value: true, kind: "danger" },
      { label: "Anuluj", value: false },
    ],
  });

// Wynik operacji na plikach: {name} albo {error}.
function check(json) {
  const result = JSON.parse(json);
  if (result.error) setStatus(result.error, "error");
  return result.error ? null : result.name;
}

async function newFile() {
  const name = await askText("Nowy plik", "");
  if (!name || !bridge) return;
  saveNow();
  const created = check(bridge.createFile(project, name));
  if (created) applyState(loadState(), created);
}

async function fileMenu() {
  const action = await dialog({
    title: current,
    actions: [
      { label: "Zmień nazwę", value: "rename" },
      { label: "Usuń plik", value: "delete", kind: "danger" },
      { label: "Anuluj", value: null },
    ],
  });
  if (!bridge) return;
  saveNow();

  if (action === "rename") {
    const name = await askText("Nowa nazwa", current);
    const renamed = name && name !== current && check(bridge.renameFile(project, current, name));
    if (renamed) applyState(loadState(), renamed);
  } else if (action === "delete" && (await confirmDelete(`Usunąć ${current}?`))) {
    if (check(bridge.deleteFile(project, current))) applyState(loadState());
  }
}

async function projectMenu() {
  const action = await dialog({
    title: "Projekty",
    actions: [
      ...projects.map((name) => ({ label: (name === project ? "● " : "") + name, value: { open: name } })),
      { label: "+ Nowy projekt", value: "new", kind: "primary" },
      { label: `Usuń „${project}”`, value: "delete", kind: "danger" },
      { label: "Anuluj", value: null },
    ],
  });
  if (!action || !bridge) return;
  saveNow();

  if (action.open) {
    if (action.open !== project) applyState(JSON.parse(bridge.openProject(action.open)));
  } else if (action === "new") {
    const name = await askText("Nazwa projektu");
    const created = name && check(bridge.createProject(name));
    if (created) applyState(JSON.parse(bridge.openProject(created)));
  } else if (await confirmDelete(`Usunąć projekt „${project}” razem z plikami i obrazkami?`)) {
    if (check(bridge.deleteProject(project))) {
      files.clear();
      current = null;
      applyState(loadState());
    }
  }
}

projectButton.addEventListener("click", projectMenu);

// Po powrocie do aplikacji: pliki mogły zmienić się z zewnątrz.
window.onResumed = () => {
  saveNow();
  applyState(loadState());
};

// --- Kompilacja i błędy ---

playButton.addEventListener("click", () => {
  if (!bridge) return;
  saveNow();
  playButton.disabled = true;
  setStatus("Kompiluję…");
  // Oddajemy klatkę przeglądarce, żeby status zdążył się odświeżyć.
  setTimeout(() => bridge.compile(project), 16);
});

function showDiagnostics(diagnostics) {
  for (const [name, entry] of files) {
    const state = name === current ? view.state : entry.state;
    const length = state.doc.length;
    const list = diagnostics
      .filter((d) => d.file === name)
      .map((d) => ({
        from: Math.min(d.from, length),
        to: Math.min(Math.max(d.to, d.from), length),
        severity: d.severity,
        message: d.message,
      }));
    const spec = setDiagnostics(state, list);
    if (name === current) view.dispatch(spec);
    else entry.state = state.update(spec).state;
    entry.hasError = list.some((d) => d.severity === "error");
  }
  renderTabs();
}

function jumpTo(diagnostic) {
  if (!files.has(diagnostic.file)) return;
  openFile(diagnostic.file);
  const position = Math.min(diagnostic.from, view.state.doc.length);
  view.dispatch({ selection: { anchor: position }, scrollIntoView: true });
  view.focus();
}

function showErrors(rows) {
  errorsPanel.replaceChildren();
  errorsPanel.hidden = rows.length === 0;
  for (const row of rows) {
    const button = document.createElement("button");
    button.textContent = row.text;
    button.className = row.severity || "";
    if (row.diagnostic) button.addEventListener("click", () => jumpTo(row.diagnostic));
    errorsPanel.append(button);
  }
}

window.onCompileResult = (result) => {
  playButton.disabled = false;
  showDiagnostics(result.diagnostics);

  const errors = result.diagnostics.filter((d) => d.severity === "error");
  if (result.ok) {
    setStatus(`OK (${result.ms} ms)`, "ok");
    showErrors([]);
    return;
  }

  const rows = result.diagnostics.map((d) => ({
    text: `${d.file}:${d.line}  ${d.message}`,
    severity: d.severity,
    diagnostic: d,
  }));
  if (result.message) rows.unshift({ text: result.message });
  showErrors(rows);

  setStatus(errors.length ? `Błędy: ${errors.length}` : result.message || "Błąd", "error");
  if (errors.length) jumpTo(errors[0]);
};

// --- Pomoc: help.html w nakładce, żeby nie tracić stanu edytora. Wstecz ją zamyka. ---
const helpPanel = document.getElementById("help");
const helpFrame = document.getElementById("helpFrame");

function openHelp() {
  saveNow();
  if (!helpFrame.getAttribute("src")) helpFrame.src = "help.html";
  helpPanel.hidden = false;
  bridge?.setHelpOpen(true);
}

window.closeHelp = () => {
  helpPanel.hidden = true;
  bridge?.setHelpOpen(false);
};

document.getElementById("helpButton").addEventListener("click", openHelp);
document.getElementById("helpClose").addEventListener("click", window.closeHelp);

// Pasek symboli, których brakuje na klawiaturze telefonu.
for (const button of document.querySelectorAll("#symbols button")) {
  button.addEventListener("mousedown", (e) => e.preventDefault()); // nie zabieraj fokusu edytorowi
  button.addEventListener("click", () => {
    const text = button.dataset.insert ?? button.textContent;
    view.dispatch(view.state.replaceSelection(text));
    view.focus();
  });
}

applyState(loadState());
