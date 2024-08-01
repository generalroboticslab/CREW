# Configuration file for the Sphinx documentation builder.
#
# For the full list of built-in configuration values, see the documentation:
# https://www.sphinx-doc.org/en/master/usage/configuration.html

import datetime

# -- Project information -----------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#project-information

project = "crew-algorithms"
copyright = f"2023-{datetime.date.today().year}, General Robotics Lab"
author = ""

# -- General configuration ---------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#general-configuration

extensions = [
    "sphinx.ext.autosummary",
    "sphinx.ext.napoleon",
    "sphinx.ext.autodoc",
    "sphinx.ext.intersphinx",
    "sphinx.ext.mathjax",
    "sphinx_remove_toctrees",
    "sphinxcontrib.video",
]

templates_path = ["_templates"]
exclude_patterns = ["_build", "Thumbs.db", ".DS_Store"]

autosummary_generate = True
autodoc_inherit_docstrings = True
autodoc_mock_imports = ["torchrl"]
napoleon_google_docstring = True
napoleon_numpy_docstring = False
add_module_names = False
intersphinx_mapping = {
    "python": ("https://docs.python.org/3", None),
    "torchrl": ("https://pytorch.org/rl", None),
}
remove_from_toctrees = ["api/generated/*"]

# -- Options for HTML output -------------------------------------------------
# https://www.sphinx-doc.org/en/master/usage/configuration.html#options-for-html-output

html_theme = "furo"
html_static_path = ["_static"]
